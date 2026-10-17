using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Synthia.Contracts.Events;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Catalog;
using Synthia.Terminal.Core.Fiscal;
using Synthia.Terminal.Core.Sales;
using Synthia.Terminal.Core.Storage;
using Synthia.Terminal.Core.Sync;

namespace Synthia.Simulator;

/// <summary>Um caixa real (Terminal.Core + SQLite) com rede e internet controláveis.</summary>
public sealed class SimTerminal
{
    public string Id { get; }
    public RecordingLink Link { get; }
    public SimulatedNfce Nfce { get; } = new();
    public LocalDb Db { get; }
    public Outbox Outbox { get; }
    public LocalCatalog Catalog { get; }
    public OutboxPusher Pusher { get; }
    public CatalogPuller Puller { get; }
    public ShiftService Shifts { get; }
    public SaleService Sales { get; }

    public SimTerminal(string id, string dir, Uri server, Recorder recorder, FakeTimeProvider clock)
    {
        Id = id;
        Link = new RecordingLink(id, recorder);
        var options = new TerminalOptions(id, Path.Combine(dir, $"{id}.db"), server);
        Db = new LocalDb(options);
        Db.EnsureCreated();

        var http = new HttpClient(Link) { BaseAddress = server, Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Add("X-Terminal-Id", id);
        if (Environment.GetEnvironmentVariable($"SYNTHIA_TERMINAL_KEY_{id}") is { Length: > 0 } key)
            http.DefaultRequestHeaders.Add("X-Terminal-Key", key);
        var client = new SyncClient(http);
        Outbox = new Outbox(options, clock);
        Catalog = new LocalCatalog(Db, clock);
        Pusher = new OutboxPusher(Db, Outbox, client, NullLogger<OutboxPusher>.Instance);
        Puller = new CatalogPuller(Catalog, client, NullLogger<CatalogPuller>.Instance);
        Shifts = new ShiftService(Db, Outbox, Pusher);
        Sales = new SaleService(Db, Outbox, Pusher, Nfce);
    }

    /// <summary>Uma rodada do worker de envio. Falha de rede é engolida, como no worker real.</summary>
    public async Task<string> PushRoundAsync()
    {
        try
        {
            var sent = await Pusher.PushPendingAsync();
            return $"{Id}: worker enviou {sent} evento(s)";
        }
        catch (HttpRequestException ex)
        {
            return $"{Id}: envio falhou ({ex.Message}); eventos continuam na outbox, próxima tentativa com backoff";
        }
    }

    public async Task<string> PullRoundAsync()
    {
        try
        {
            var v = await Puller.PullAsync();
            return $"{Id}: catálogo local na versão {v}";
        }
        catch (HttpRequestException ex)
        {
            return $"{Id}: catálogo não atualizado ({ex.Message}); segue na versão {Catalog.CurrentVersion()}";
        }
    }
}

/// <summary>
/// Substitui a ACBr na simulação: autoriza com internet, emite em contingência sem ela.
/// A chave gerada é fictícia.
/// </summary>
public sealed class SimulatedNfce : INfceEmitter
{
    private int _number;
    public bool InternetUp { get; set; } = true;

    public Task<NfceResult> EmitAsync(SaleCompleted sale, CancellationToken ct = default)
    {
        var key = $"3526100000000000000065001{++_number:D9}{(InternetUp ? 1 : 9)}00000000";
        return Task.FromResult(new NfceResult(key, InternetUp ? NfceStatus.Authorized : NfceStatus.Pending));
    }
}
