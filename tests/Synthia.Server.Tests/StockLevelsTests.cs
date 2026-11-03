using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Synthia.Contracts;
using Synthia.Contracts.Events;
using Synthia.Server.Sync;
using static Synthia.Server.Tests.EventBuilder;

namespace Synthia.Server.Tests;

/// <summary>O saldo mantido a cada evento precisa dar sempre o mesmo que a regra do ledger.</summary>
public sealed class StockLevelsTests(ServerFixture fx) : IClassFixture<ServerFixture>
{
    private static readonly DateTimeOffset T = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private EventStore Store => fx.Factory.Services.GetRequiredService<EventStore>();

    private static EventEnvelope SaleAt(DateTimeOffset at, params (Guid ProductId, decimal Qty)[] items)
    {
        var saleItems = items.Select(i => new SaleItem(Guid.CreateVersion7(), i.ProductId, "Produto", i.Qty, 100, (long)(i.Qty * 100))).ToList();
        var total = saleItems.Sum(i => i.TotalCents);
        var sale = new SaleCompleted(Guid.CreateVersion7(), Guid.CreateVersion7(), saleItems,
            [new SalePayment(PaymentMethods.Cash, total)], total, 0, null, NfceStatus.Authorized);
        return new EventEnvelope(Uuid7.Next(), sale.SaleId, 1, EventTypes.SaleCompleted, "CX01", at, 1, SynthiaJson.ToElement(sale));
    }

    private static EventEnvelope CountAt(DateTimeOffset at, Guid productId, decimal qty) =>
        Make(EventTypes.StockCounted, Guid.CreateVersion7(), 1, new StockCounted(productId, qty), at);

    private async Task<decimal> BalanceAsync(Guid productId)
    {
        await using var conn = await fx.Db.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<decimal?>("SELECT quantity FROM stock_balances WHERE product_id = @productId", new { productId }) ?? 0;
    }

    /// <summary>Saldo calculado do zero pelo ledger, com a regra da view antiga.</summary>
    private async Task<decimal> LedgerAsync(Guid productId)
    {
        await using var conn = await fx.Db.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<decimal>(
            """
            WITH lc AS (SELECT quantity, occurred_at FROM stock_movements WHERE product_id = @productId AND kind = 'count'
                        ORDER BY occurred_at DESC, id DESC LIMIT 1)
            SELECT coalesce((SELECT quantity FROM lc), 0)
                 + coalesce((SELECT sum(quantity) FROM stock_movements m WHERE m.product_id = @productId AND m.kind <> 'count'
                             AND ((SELECT occurred_at FROM lc) IS NULL OR m.occurred_at > (SELECT occurred_at FROM lc))), 0)
            """, new { productId });
    }

    [Fact]
    public async Task Venda_atrasada_de_caixa_offline_anterior_a_contagem_nao_mexe_no_saldo()
    {
        var p = Guid.CreateVersion7();
        await Store.AppendAsync([CountAt(T, p, 10)]);

        await Store.AppendAsync([SaleAt(T.AddHours(-1), (p, 2))]);
        Assert.Equal(10, await BalanceAsync(p));

        await Store.AppendAsync([SaleAt(T.AddHours(1), (p, 3))]);
        Assert.Equal(7, await BalanceAsync(p));
    }

    [Fact]
    public async Task Contagem_recalcula_com_as_vendas_posteriores_e_contagem_mais_antiga_e_ignorada()
    {
        var p = Guid.CreateVersion7();
        await Store.AppendAsync([SaleAt(T.AddHours(-2), (p, 1)), SaleAt(T.AddHours(2), (p, 4))]);
        Assert.Equal(-5, await BalanceAsync(p));

        await Store.AppendAsync([CountAt(T, p, 20)]);
        Assert.Equal(16, await BalanceAsync(p));

        await Store.AppendAsync([CountAt(T.AddDays(-1), p, 99)]);
        Assert.Equal(16, await BalanceAsync(p));
        Assert.Equal(await LedgerAsync(p), await BalanceAsync(p));
    }

    [Fact]
    public async Task Caixas_sincronizando_ao_mesmo_tempo_os_mesmos_produtos_nao_travam_nem_perdem_venda()
    {
        var products = Enumerable.Range(0, 6).Select(_ => Guid.CreateVersion7()).ToArray();
        await Store.AppendAsync([CountAt(T, products[0], 1000)]);

        // Cada lote vende os produtos numa ordem diferente; contagem no meio de tudo.
        var batches = Enumerable.Range(0, 24).Select(i =>
        {
            var order = i % 2 == 0 ? products : products.Reverse().ToArray();
            var at = T.AddMinutes(10 + i);
            return (IReadOnlyList<EventEnvelope>)[SaleAt(at, order.Select(p => (p, 1m)).ToArray()), SaleAt(at.AddSeconds(1), (order[2], 2))];
        }).ToList();
        var tasks = batches.Select(b => Task.Run(() => Store.AppendAsync(b))).ToList();
        tasks.Add(Task.Run(() => Store.AppendAsync([CountAt(T.AddMinutes(5), products[1], 500)])));
        await Task.WhenAll(tasks);

        foreach (var p in products)
            Assert.Equal(await LedgerAsync(p), await BalanceAsync(p));
        Assert.Equal(1000 - 24, await BalanceAsync(products[0]));
        Assert.Equal(500 - 24, await BalanceAsync(products[1]));
    }

    [Fact]
    public async Task Rebuild_chega_no_mesmo_saldo_que_o_mantido_evento_a_evento()
    {
        var p = Guid.CreateVersion7();
        await Store.AppendAsync([SaleAt(T, (p, 2)), CountAt(T.AddHours(1), p, 30), SaleAt(T.AddHours(2), (p, 5))]);
        var before = await BalanceAsync(p);

        await Store.RebuildProjectionsAsync();

        Assert.Equal(25, before);
        Assert.Equal(before, await BalanceAsync(p));
    }
}
