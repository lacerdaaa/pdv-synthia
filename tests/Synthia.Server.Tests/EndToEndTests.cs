using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Catalog;
using Synthia.Terminal.Core.Fiscal;
using Synthia.Terminal.Core.Sales;
using Synthia.Terminal.Core.Storage;
using Synthia.Terminal.Core.Sync;

namespace Synthia.Server.Tests;

/// <summary>Caixa de verdade (SQLite) falando com o servidor de verdade (Postgres).</summary>
public sealed class EndToEndTests(ServerFixture fx) : IClassFixture<ServerFixture>, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"synthia-e2e-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" }) File.Delete(f);
    }

    [Fact]
    public async Task Caixa_baixa_catalogo_vende_e_servidor_projeta_estoque()
    {
        var http = fx.Factory.CreateClient();
        var leite = new ProductData(Guid.CreateVersion7(), "Leite 1L", "7890001", null, "UN", 549, "04012010", true);
        (await http.PutAsJsonAsync($"/backoffice/products/{leite.Id}", leite)).EnsureSuccessStatusCode();

        var options = new TerminalOptions("CX02", _dbPath, http.BaseAddress!);
        var db = new LocalDb(options);
        db.EnsureCreated();
        var client = new SyncClient(http);
        var outbox = new Outbox(options, TimeProvider.System);
        var catalog = new LocalCatalog(db, TimeProvider.System);
        var pusher = new OutboxPusher(db, outbox, client, NullLogger<OutboxPusher>.Instance);
        var puller = new CatalogPuller(catalog, client, NullLogger<CatalogPuller>.Instance);
        var shifts = new ShiftService(db, outbox, pusher);
        var sales = new SaleService(db, outbox, pusher, new AlwaysAuthorized());

        await puller.PullAsync();
        shifts.Open("João", 5000);
        var cart = new Cart();
        cart.Add(catalog.FindByBarcode("7890001")!, 3);
        var sale = await sales.CompleteAsync(cart, [new SalePayment(PaymentMethods.Cash, 2000)]);

        await pusher.PushPendingAsync();
        await pusher.PushPendingAsync(); // nada pendente: no-op

        await using var conn = await fx.Db.OpenConnectionAsync();
        Assert.Equal(-3m, await conn.ExecuteScalarAsync<decimal>(
            "SELECT quantity FROM stock_balances WHERE product_id = @Id", new { leite.Id }));
        var shift = await conn.QuerySingleAsync<(long Total, long Cash)>(
            "SELECT sales_total_cents, cash_sales_cents FROM shift_totals WHERE shift_id = @ShiftId", new { sale.ShiftId });
        Assert.Equal((1647L, 1647L), shift);
        Assert.Equal(0, pusher.PendingCount());
    }

    private sealed class AlwaysAuthorized : INfceEmitter
    {
        public Task<NfceResult> EmitAsync(SaleCompleted sale, CancellationToken ct = default) =>
            Task.FromResult(new NfceResult("35261000000000000000650010000000011000000010", NfceStatus.Authorized));
    }
}
