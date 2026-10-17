using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Synthia.Terminal.Core.Storage;

namespace Synthia.Terminal.Core.Sync;

/// <summary>
/// Worker em background que empurra a outbox para o servidor. O operador nunca espera por ele:
/// a cada 2 s, ou logo após cada venda, envia lotes de até 100 eventos. Em erro, backoff
/// exponencial de 2 s até 60 s.
/// </summary>
public sealed class OutboxPusher(LocalDb db, Outbox outbox, SyncClient client, ILogger<OutboxPusher> logger)
    : BackgroundService, IOutboxSignal
{
    public const int BatchSize = 100;
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _wake = new(0, 1);

    public void Notify()
    {
        try { _wake.Release(); }
        catch (SemaphoreFullException) { /* já há um aviso pendente */ }
    }

    /// <summary>Indicador da tela: eventos pendentes de envio. Algumas centenas = algo errado.</summary>
    public int PendingCount()
    {
        using var conn = db.Open();
        return Outbox.PendingCount(conn);
    }

    /// <summary>Envia tudo que estiver pendente. Devolve quantos eventos o servidor confirmou.</summary>
    public async Task<int> PushPendingAsync(CancellationToken ct = default)
    {
        var sent = 0;
        while (true)
        {
            using var conn = db.Open();
            var batch = Outbox.TakePending(conn, BatchSize);
            if (batch.Count == 0) return sent;

            var accepted = await client.PushEventsAsync(batch, ct);
            outbox.MarkSent(conn, accepted);
            sent += accepted.Count;

            if (batch.Count < BatchSize || accepted.Count == 0) return sent;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var delay = Interval;
        var lastPrune = DateTimeOffset.MinValue;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PushPendingAsync(ct);
                delay = Interval;

                if (DateTimeOffset.UtcNow - lastPrune > TimeSpan.FromHours(1))
                {
                    using var conn = db.Open();
                    outbox.Prune(conn);
                    lastPrune = DateTimeOffset.UtcNow;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxBackoff.Ticks));
                logger.LogWarning(ex, "Falha ao enviar outbox; nova tentativa em {Delay}", delay);
            }

            try { await _wake.WaitAsync(delay, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
