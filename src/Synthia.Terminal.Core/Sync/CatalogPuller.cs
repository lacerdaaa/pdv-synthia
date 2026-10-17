using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Synthia.Terminal.Core.Catalog;

namespace Synthia.Terminal.Core.Sync;

/// <summary>
/// A cada 30 s puxa o delta do catálogo desde <c>last_catalog_version</c>. Se o servidor
/// pedir, baixa o snapshot completo. Servidor fora do ar: o catálogo congela na última versão.
/// </summary>
public sealed class CatalogPuller(LocalCatalog catalog, SyncClient client, ILogger<CatalogPuller> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>Atualiza o catálogo até a última versão do servidor. Devolve a versão local final.</summary>
    public async Task<long> PullAsync(CancellationToken ct = default)
    {
        while (true)
        {
            var since = catalog.CurrentVersion();
            var delta = await client.GetCatalogDeltaAsync(since, ct);

            if (delta.SnapshotRequired)
            {
                logger.LogInformation("Baixando snapshot do catálogo (local {Local}, servidor {Latest})", since, delta.LatestVersion);
                catalog.ApplySnapshot(await client.GetCatalogSnapshotAsync(ct));
                return catalog.CurrentVersion();
            }

            catalog.ApplyDelta(delta.Changes);

            var now = catalog.CurrentVersion();
            if (delta.Changes.Count == 0 || now >= delta.LatestVersion) return now;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await PullAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Falha ao atualizar catálogo; mantendo versão {Version}", catalog.CurrentVersion());
            }
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }
}
