using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Synthia.Contracts;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;
using Synthia.Contracts.Sync;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Catalog;
using Synthia.Terminal.Core.Fiscal;
using Synthia.Terminal.Core.Sales;
using Synthia.Terminal.Core.Storage;
using Synthia.Terminal.Core.Sync;

namespace Synthia.Terminal.Tests;

/// <summary>Um caixa completo sobre um SQLite temporário, com relógio e servidor falsos.</summary>
public sealed class TestTerminal : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"synthia-{Guid.NewGuid():N}.db");

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
    public FakeServer Server { get; } = new();
    public FakeNfce Nfce { get; } = new();
    public LocalDb Db { get; }
    public Outbox Outbox { get; }
    public LocalCatalog Catalog { get; }
    public OutboxPusher Pusher { get; }
    public CatalogPuller Puller { get; }
    public ShiftService Shifts { get; }
    public SaleService Sales { get; }
    public StoreProfileCache Store { get; }
    public StoreProfilePuller StorePuller { get; }

    public TestTerminal()
    {
        var options = new TerminalOptions("CX01", _path, new Uri("http://servidor.local"));
        Db = new LocalDb(options);
        Db.EnsureCreated();

        var client = new SyncClient(new HttpClient(Server) { BaseAddress = options.ServerUrl });
        Outbox = new Outbox(options, Clock);
        Catalog = new LocalCatalog(Db, Clock);
        Pusher = new OutboxPusher(Db, Outbox, client, NullLogger<OutboxPusher>.Instance);
        Puller = new CatalogPuller(Catalog, client, NullLogger<CatalogPuller>.Instance);
        Shifts = new ShiftService(Db, Outbox, Pusher);
        Sales = new SaleService(Db, Outbox, Pusher, Nfce);
        Store = new StoreProfileCache(Db, new StoreSection { Name = "Loja do terminal.json", Cnpj = "11.222.333/0001-81" }, _path + ".logo");
        StorePuller = new StoreProfilePuller(Store, client, NullLogger<StoreProfilePuller>.Instance);
    }

    public ProductData SeedProduct(string name, long cents, string? barcode = null, int? plu = null, long version = 1)
    {
        var p = new ProductData(Guid.CreateVersion7(), name, barcode, plu, plu is null ? "UN" : "KG", cents, "00000000", true);
        Catalog.ApplyDelta([new CatalogChange(version, CatalogEntities.Product, p.Id, SynthiaJson.ToElement(p))]);
        return p;
    }

    public int Pending() => Pusher.PendingCount();

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm", _path + ".logo" })
            File.Delete(f);
    }
}

public sealed class FakeNfce : INfceEmitter
{
    public string Status { get; set; } = NfceStatus.Authorized;

    public Task<NfceResult> EmitAsync(SaleCompleted sale, CancellationToken ct = default) =>
        Task.FromResult(new NfceResult($"NFCE-{sale.SaleId:N}", Status));
}

/// <summary>Servidor em memória com o mesmo contrato de /sync: idempotente por event_id.</summary>
public sealed class FakeServer : HttpMessageHandler
{
    public bool Online { get; set; } = true;
    public Dictionary<Guid, EventEnvelope> Events { get; } = [];
    public List<CatalogChange> Changes { get; } = [];
    public bool ForceSnapshot { get; set; }
    public int PushCalls { get; private set; }
    public StoreProfile? Store { get; set; }
    public byte[]? StoreLogo { get; set; }
    public int LogoDownloads { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!Online) throw new HttpRequestException("servidor fora do ar");

        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Post && path == SyncRoutes.Events)
        {
            PushCalls++;
            var body = (await request.Content!.ReadFromJsonAsync<PushEventsRequest>(SynthiaJson.Options, ct))!;
            foreach (var e in body.Events) Events.TryAdd(e.EventId, e);
            return Json(new PushEventsResponse(body.Events.Select(e => e.EventId).ToList()));
        }

        if (path == SyncRoutes.Store && Store is not null) return Json(Store);
        if (path == SyncRoutes.StoreLogo && StoreLogo is not null)
        {
            LogoDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(StoreLogo) };
        }

        var latest = Changes.Count == 0 ? 0 : Changes.Max(c => c.Version);
        if (path == SyncRoutes.CatalogSnapshot)
        {
            var products = Changes.Where(c => c.Entity == CatalogEntities.Product)
                .GroupBy(c => c.EntityId).Select(g => SynthiaJson.FromElement<ProductData>(g.MaxBy(c => c.Version)!.Data)).ToList();
            return Json(new CatalogSnapshotResponse(latest, products, []));
        }

        if (path == SyncRoutes.Catalog)
        {
            var since = long.Parse(request.RequestUri.Query.Split('=')[1]);
            var snapshot = ForceSnapshot || (since == 0 && latest > 0);
            return Json(new CatalogDeltaResponse(since, latest, snapshot,
                snapshot ? [] : Changes.Where(c => c.Version > since).OrderBy(c => c.Version).ToList()));
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value, options: SynthiaJson.Options) };
}
