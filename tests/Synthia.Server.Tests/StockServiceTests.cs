using Microsoft.Extensions.DependencyInjection;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;
using Synthia.Server.Backoffice;
using Synthia.Server.Catalog;
using static Synthia.Server.Tests.EventBuilder;

namespace Synthia.Server.Tests;

public sealed class StockServiceTests(ServerFixture fx) : IClassFixture<ServerFixture>
{
    private T Get<T>() where T : notnull => fx.Factory.Services.GetRequiredService<T>();

    private async Task<ProductData> NewProductAsync(string name)
    {
        var p = new ProductData(Guid.CreateVersion7(), name, null, null, "UN", 1000, "00000000", true);
        await Get<CatalogService>().UpsertProductAsync(p);
        return p;
    }

    [Fact]
    public async Task Transferencia_da_loja_antiga_entra_no_estoque()
    {
        var arroz = await NewProductAsync($"Arroz {Guid.NewGuid():N}");
        var stock = Get<StockService>();

        var receipt = await stock.ReceiveAsync(StockReceiptSources.Transfer, null, "ROM-001",
            [new StockReceiptItem(arroz.Id, arroz.Name, 48, null)], "gerente", "mudança");

        var balance = (await stock.BalancesAsync(arroz.Name)).Single();
        Assert.Equal(48, balance.Quantity);
        var listed = (await stock.ListReceiptsAsync()).Single(r => r.ReceiptId == receipt.ReceiptId);
        Assert.Equal("ROM-001", listed.Document);
        Assert.Equal(StockReceiptSources.Transfer, listed.Source);
    }

    [Fact]
    public async Task Venda_depois_da_entrada_baixa_e_historico_mostra_tudo()
    {
        var feijao = await NewProductAsync($"Feijão {Guid.NewGuid():N}");
        var stock = Get<StockService>();
        await stock.ReceiveAsync(StockReceiptSources.Supplier, "Distribuidora X", "NF 123",
            [new StockReceiptItem(feijao.Id, feijao.Name, 10, 650)], "gerente", null);
        var (sale, _) = Sale(Guid.CreateVersion7(), (feijao.Id, 3, 1000));
        await Get<Synthia.Server.Sync.EventStore>().AppendAsync([sale]);

        Assert.Equal(7, (await stock.BalancesAsync(feijao.Name)).Single().Quantity);
        var history = await stock.HistoryAsync(feijao.Id);
        Assert.Equal(["sale", "received"], history.Select(h => h.Kind));
        Assert.Equal(BackofficeEvents.TerminalId, history[1].TerminalId);
    }

    [Fact]
    public async Task Produto_sem_movimento_aparece_com_zero_e_filtro_de_negativos()
    {
        var parado = await NewProductAsync($"Parado {Guid.NewGuid():N}");
        var vendido = await NewProductAsync($"Vendido {Guid.NewGuid():N}");
        var (sale, _) = Sale(Guid.CreateVersion7(), (vendido.Id, 2, 1000));
        await Get<Synthia.Server.Sync.EventStore>().AppendAsync([sale]);
        var stock = Get<StockService>();

        Assert.Equal(0, (await stock.BalancesAsync(parado.Name)).Single().Quantity);
        var negatives = await stock.BalancesAsync(onlyNegative: true);
        Assert.Contains(negatives, b => b.ProductId == vendido.Id);
        Assert.DoesNotContain(negatives, b => b.ProductId == parado.Id);
    }

    [Fact]
    public async Task Entrada_sem_itens_ou_com_quantidade_invalida_e_recusada()
    {
        var stock = Get<StockService>();
        await Assert.ThrowsAsync<ArgumentException>(() => stock.ReceiveAsync(StockReceiptSources.Other, null, null, [], "x", null));
        await Assert.ThrowsAsync<ArgumentException>(() => stock.ReceiveAsync(StockReceiptSources.Other, null, null,
            [new StockReceiptItem(Guid.NewGuid(), "x", 0, null)], "x", null));
    }

    [Fact]
    public async Task Importacao_grava_produtos_em_lote()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var plan = ProductImport.Plan($"codigo_barras;descricao;preco\n;Sem código;1\n7890000{suffix[..5]}1;Lote A {suffix};1,00\n", []);
        var catalog = Get<CatalogService>();

        await catalog.UpsertProductsAsync(plan.Where(r => r.Product is not null).Select(r => r.Product!).ToList());

        Assert.Contains(await catalog.ListProductsAsync(), p => p.Name == $"Lote A {suffix}");
    }
}
