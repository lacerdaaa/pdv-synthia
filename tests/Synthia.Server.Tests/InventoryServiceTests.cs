using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;
using Synthia.Server.Backoffice;
using Synthia.Server.Catalog;
using Synthia.Server.Sync;
using static Synthia.Server.Tests.EventBuilder;

namespace Synthia.Server.Tests;

/// <summary>Cada teste usa um banco próprio: inventário geral mexe em todos os produtos.</summary>
public sealed class InventoryServiceTests : IAsyncLifetime
{
    private readonly ServerFixture fx = new();

    public Task InitializeAsync() => fx.InitializeAsync();
    public Task DisposeAsync() => fx.DisposeAsync();

    private T Get<T>() where T : notnull => fx.Factory.Services.GetRequiredService<T>();

    private async Task<ProductData> ProductAsync(string name, string unit = "UN")
    {
        var p = new ProductData(Guid.CreateVersion7(), name, null, null, unit, 1000, "00000000", true);
        await Get<CatalogService>().UpsertProductAsync(p);
        return p;
    }

    private async Task<decimal> BalanceAsync(Guid productId) =>
        (await Get<StockService>().BalancesAsync()).Single(b => b.ProductId == productId).Quantity;

    [Fact]
    public async Task Bipar_soma_e_corrigir_substitui()
    {
        var arroz = await ProductAsync("Arroz");
        var inv = Get<InventoryService>();
        var id = await inv.CreateAsync("Mercearia", fullCount: false, "estoquista");

        await inv.AddAsync(id, arroz.Id, 1, "estoquista");
        await inv.AddAsync(id, arroz.Id, 1, "estoquista");
        await inv.AddAsync(id, arroz.Id, 10, "estoquista");
        Assert.Equal(12, (await inv.LinesAsync(id)).Single().Quantity);

        await inv.SetAsync(id, arroz.Id, 11, "gerente");
        var line = (await inv.LinesAsync(id)).Single();
        Assert.Equal(11, line.Quantity);
        Assert.Equal("gerente", line.CountedBy);
    }

    [Fact]
    public async Task Revisao_mostra_esperado_contado_e_diferenca()
    {
        var feijao = await ProductAsync("Feijão");
        await Get<StockService>().ReceiveAsync(StockReceiptSources.Transfer, null, null, [new StockReceiptItem(feijao.Id, "Feijão", 50, null)], "g", null);
        var inv = Get<InventoryService>();
        var id = await inv.CreateAsync("Mercearia", false, "e");
        await inv.AddAsync(id, feijao.Id, 47, "e");

        var line = (await inv.ReviewAsync(id)).Single();

        Assert.Equal(50, line.Expected);
        Assert.Equal(47, line.Counted);
        Assert.Equal(-3, line.Difference);
    }

    [Fact]
    public async Task Confirmar_grava_contagem_e_venda_depois_da_contagem_continua_valendo()
    {
        var cafe = await ProductAsync("Café");
        var inv = Get<InventoryService>();
        var id = await inv.CreateAsync("Café", false, "e");
        await inv.AddAsync(id, cafe.Id, 20, "e");

        // Venda no caixa depois do item ser contado, antes do inventário ser confirmado.
        var (sale, _) = Sale(Guid.CreateVersion7(), (cafe.Id, 2, 1000));
        await Get<EventStore>().AppendAsync([sale with { OccurredAt = DateTimeOffset.UtcNow.AddSeconds(5) }]);

        Assert.Equal(1, await inv.ConfirmAsync(id, "gerente"));

        Assert.Equal(18, await BalanceAsync(cafe.Id));
        Assert.Equal(InventoryService.Confirmed, (await inv.GetAsync(id))!.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => inv.AddAsync(id, cafe.Id, 1, "e"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => inv.ConfirmAsync(id, "gerente"));
    }

    [Fact]
    public async Task Inventario_geral_zera_o_que_tem_saldo_e_nao_foi_contado()
    {
        var contado = await ProductAsync("Contado");
        var esquecido = await ProductAsync("Esquecido");
        var semSaldo = await ProductAsync("Sem saldo");
        await Get<StockService>().ReceiveAsync(StockReceiptSources.Transfer, null, null,
            [new StockReceiptItem(contado.Id, "Contado", 10, null), new StockReceiptItem(esquecido.Id, "Esquecido", 7, null)], "g", null);
        var inv = Get<InventoryService>();
        var id = await inv.CreateAsync("Geral véspera", fullCount: true, "g");
        await inv.AddAsync(id, contado.Id, 9, "g");

        var review = await inv.ReviewAsync(id);
        Assert.Equal(2, review.Count);
        Assert.False(review.Single(r => r.ProductId == esquecido.Id).WasCounted);

        await inv.ConfirmAsync(id, "g");

        Assert.Equal(9, await BalanceAsync(contado.Id));
        Assert.Equal(0, await BalanceAsync(esquecido.Id));
        Assert.Equal(0, await BalanceAsync(semSaldo.Id));
    }

    [Fact]
    public async Task Contagem_guarda_inventario_e_saldo_esperado_no_evento()
    {
        var leite = await ProductAsync("Leite");
        var inv = Get<InventoryService>();
        var id = await inv.CreateAsync("Frios", false, "e");
        await inv.AddAsync(id, leite.Id, 5, "joana");
        await inv.ConfirmAsync(id, "gerente");

        await using var conn = await fx.Db.OpenConnectionAsync();
        var json = await conn.QuerySingleAsync<string>("SELECT payload::text FROM events WHERE type = 'StockCounted' AND stream_id = @Id", new { leite.Id });
        var counted = System.Text.Json.JsonSerializer.Deserialize<StockCounted>(json, Synthia.Contracts.SynthiaJson.Options)!;
        Assert.Equal(id, counted.InventoryId);
        Assert.Equal(0, counted.ExpectedQuantity);
        Assert.Equal("joana", counted.CountedBy);
    }

    [Fact]
    public async Task Descartar_nao_mexe_no_estoque()
    {
        var p = await ProductAsync("Descartado");
        var inv = Get<InventoryService>();
        var id = await inv.CreateAsync("Teste", false, "e");
        await inv.AddAsync(id, p.Id, 3, "e");

        await inv.DiscardAsync(id, "e");

        Assert.Equal(InventoryService.Discarded, (await inv.GetAsync(id))!.Status);
        Assert.Equal(0, await BalanceAsync(p.Id));
    }
}
