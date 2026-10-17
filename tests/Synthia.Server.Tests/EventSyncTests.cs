using System.Net.Http.Json;
using Dapper;
using Synthia.Contracts.Events;
using Synthia.Contracts.Sync;
using static Synthia.Server.Tests.EventBuilder;

namespace Synthia.Server.Tests;

public sealed class EventSyncTests(ServerFixture fx) : IClassFixture<ServerFixture>
{
    private readonly HttpClient _http = fx.Factory.CreateClient();

    private async Task<PushEventsResponse> PushAsync(params EventEnvelope[] events)
    {
        var res = await _http.PostAsJsonAsync(SyncRoutes.Events, new PushEventsRequest(events));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PushEventsResponse>())!;
    }

    private async Task<decimal> StockAsync(Guid productId)
    {
        await using var conn = await fx.Db.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<decimal?>(
            "SELECT quantity FROM stock_balances WHERE product_id = @productId", new { productId }) ?? 0;
    }

    [Fact]
    public async Task Reenvio_do_mesmo_lote_e_aceito_sem_duplicar()
    {
        var product = Guid.CreateVersion7();
        var (sale, _) = Sale(Guid.CreateVersion7(), (product, 2, 500));

        var first = await PushAsync(sale);
        var second = await PushAsync(sale);

        Assert.Equal([sale.EventId], first.Accepted);
        Assert.Equal([sale.EventId], second.Accepted);
        Assert.Equal(-2, await StockAsync(product));

        await using var conn = await fx.Db.OpenConnectionAsync();
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM events WHERE event_id = @EventId", new { sale.EventId }));
    }

    [Fact]
    public async Task Cancelamento_de_item_estorna_estoque_e_total_do_turno()
    {
        var shift = Guid.CreateVersion7();
        var arroz = Guid.CreateVersion7();
        var feijao = Guid.CreateVersion7();
        var (saleEvent, sale) = Sale(shift, (arroz, 1, 2899), (feijao, 2, 899));
        var cancel = Make(EventTypes.SaleItemCancelled, sale.SaleId, 2,
            new SaleItemCancelled(sale.SaleId, sale.Items[1].ItemId, "cliente desistiu"));

        await PushAsync(saleEvent, cancel);

        Assert.Equal(-1, await StockAsync(arroz));
        Assert.Equal(0, await StockAsync(feijao));

        await using var conn = await fx.Db.OpenConnectionAsync();
        var total = await conn.ExecuteScalarAsync<long>("SELECT sales_total_cents FROM shift_totals WHERE shift_id = @shift", new { shift });
        Assert.Equal(2899, total);
    }

    [Fact]
    public async Task Venda_abaixo_de_zero_nunca_e_bloqueada()
    {
        var product = Guid.CreateVersion7();
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var count = Make(EventTypes.StockCounted, product, 1, new StockCounted(product, 1), t0);
        var (cx1, _) = Sale(Guid.CreateVersion7(), (product, 1, 100));
        var (cx2, _) = Sale(Guid.CreateVersion7(), (product, 1, 100));

        await PushAsync(count, cx1, cx2);

        Assert.Equal(-1, await StockAsync(product));
    }

    [Fact]
    public async Task Contagem_substitui_saldo_anterior()
    {
        var product = Guid.CreateVersion7();
        var (before, _) = Sale(Guid.CreateVersion7(), (product, 3, 100));
        await PushAsync(before);

        var count = Make(EventTypes.StockCounted, product, 1, new StockCounted(product, 10));
        await PushAsync(count);

        var (after, _) = Sale(Guid.CreateVersion7(), (product, 4, 100));
        await PushAsync(after);

        Assert.Equal(6, await StockAsync(product));
    }

    [Fact]
    public async Task Nfce_em_contingencia_entra_na_fila_e_sai_ao_autorizar()
    {
        var (saleEvent, sale) = Sale(Guid.CreateVersion7(), (Guid.CreateVersion7(), 1, 100));
        var pending = saleEvent with
        {
            Payload = Synthia.Contracts.SynthiaJson.ToElement(sale with { NfceKey = "3526...", NfceStatus = NfceStatus.Pending }),
        };
        await PushAsync(pending);

        await using var conn = await fx.Db.OpenConnectionAsync();
        Assert.Equal("pending", await conn.ExecuteScalarAsync<string>("SELECT status FROM nfce_queue WHERE sale_id = @SaleId", new { sale.SaleId }));

        await PushAsync(Make(EventTypes.NfceAuthorized, sale.SaleId, 2, new NfceAuthorized(sale.SaleId, "3526...", "135260000000001")));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM nfce_queue WHERE sale_id = @SaleId", new { sale.SaleId }));
    }

    [Fact]
    public async Task Rebuild_das_projecoes_reproduz_o_mesmo_saldo()
    {
        var product = Guid.CreateVersion7();
        var (s1, _) = Sale(Guid.CreateVersion7(), (product, 2, 100));
        var (s2, sale2) = Sale(Guid.CreateVersion7(), (product, 5, 100));
        var cancel = Make(EventTypes.SaleCancelled, sale2.SaleId, 2, new SaleCancelled(sale2.SaleId, "erro"));
        await PushAsync(s1, s2, cancel);
        var before = await StockAsync(product);

        var res = await _http.PostAsync("/backoffice/projections/rebuild", null);
        res.EnsureSuccessStatusCode();

        Assert.Equal(-2, before);
        Assert.Equal(before, await StockAsync(product));
    }
}
