using Microsoft.Extensions.DependencyInjection;
using Synthia.Contracts.Catalog;
using Synthia.Server.Backoffice;
using Synthia.Server.Catalog;

namespace Synthia.Server.Tests;

public sealed class NfeImportTests : IAsyncLifetime
{
    private static readonly string Xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "nfe-exemplo.xml"));
    private readonly ServerFixture fx = new();
    private ProductData arroz = null!, feijao = null!;

    public async Task InitializeAsync()
    {
        await fx.InitializeAsync();
        arroz = new ProductData(Guid.CreateVersion7(), "Arroz Tipo 1 5kg", "7891000100103", null, "UN", 2899, "10063021", true);
        feijao = new ProductData(Guid.CreateVersion7(), "Feijão Carioca 1kg", "7891000200209", null, "UN", 899, "07133399", true);
        await Get<CatalogService>().UpsertProductsAsync([arroz, feijao]);
    }

    public Task DisposeAsync() => fx.DisposeAsync();

    private T Get<T>() where T : notnull => fx.Factory.Services.GetRequiredService<T>();

    private async Task<NfePlan> PlanAsync() => await Get<NfeImport>().PlanAsync(Xml, await Get<CatalogService>().ListProductsAsync());

    [Fact]
    public void Le_cabecalho_e_itens_do_xml()
    {
        var doc = NfeXml.Parse(Xml);

        Assert.Equal(44, doc.AccessKey.Length);
        Assert.Equal("12345", doc.Number);
        Assert.Equal("Distribuidora Exemplo", doc.SupplierName);
        Assert.Equal("12345678000195", doc.SupplierCnpj);
        Assert.Equal(647m, doc.TotalValue);
        Assert.Equal(4, doc.Items.Count);
        Assert.Null(doc.Items[3].Ean); // SEM GTIN
        Assert.Equal(12m, doc.Items[0].TaxQuantity);
    }

    [Theory]
    [InlineData("isto não é xml")]
    [InlineData("<root/>")]
    public void Arquivo_que_nao_e_nfe_e_recusado(string xml) => Assert.Throws<FormatException>(() => NfeXml.Parse(xml));

    [Fact]
    public async Task Fardo_vira_unidades_pelo_ean_tributavel_e_custo_por_unidade()
    {
        var plan = await PlanAsync();

        var a = plan.Lines[0];
        Assert.Equal(NfeMatch.TaxEan, a.MatchedBy);
        Assert.Equal(arroz.Id, a.Product!.Id);
        Assert.Equal(6, a.Factor);           // 1 FD = 6 UN
        Assert.Equal(12, a.Quantity);
        Assert.Equal(2500, a.UnitCostCents);  // R$ 300 / 12

        Assert.Equal(feijao.Id, plan.Lines[1].Product!.Id);
        Assert.Equal(24, plan.Lines[1].Quantity);
        Assert.Equal(NfeMatch.None, plan.Lines[2].MatchedBy);
        Assert.False(plan.CanReceive);
    }

    [Fact]
    public async Task Lanca_a_nota_aprende_o_vinculo_e_bloqueia_duplicada()
    {
        var cafe = new ProductData(Guid.CreateVersion7(), "Café Especial 250g", "7891000800805", null, "UN", 1990, "09012100", true);
        await Get<CatalogService>().UpsertProductAsync(cafe);
        var plan = await PlanAsync();
        var lines = plan.Lines.ToList();
        Assert.Equal(10, lines[2].Factor); // agora casou pelo EAN tributável: 1 CX = 10 UN
        lines[3] = lines[3] with { Ignored = true }; // sacola: não entra no estoque

        var receipt = await Get<NfeImport>().ReceiveAsync(plan with { Lines = lines }, "gerente");

        Assert.Equal("NF 12345/1", receipt.Document);
        Assert.Equal(plan.Document.AccessKey, receipt.AccessKey);
        var balances = await Get<StockService>().BalancesAsync();
        Assert.Equal(12, balances.Single(b => b.ProductId == arroz.Id).Quantity);
        Assert.Equal(24, balances.Single(b => b.ProductId == feijao.Id).Quantity);
        Assert.Equal(10, balances.Single(b => b.ProductId == cafe.Id).Quantity);

        var again = await PlanAsync();
        Assert.True(again.AlreadyImported);
        Assert.All(again.Lines.Take(3), l => Assert.Equal(NfeMatch.Mapping, l.MatchedBy));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Get<NfeImport>().ReceiveAsync(again, "gerente"));
    }

    [Fact]
    public async Task Vinculo_manual_com_fator_e_aprendido_para_a_proxima_nota()
    {
        var plan = await PlanAsync();
        var lines = plan.Lines.ToList();
        // Café sem cadastro de EAN: o usuário vincula manualmente a um produto existente, 1 CX = 10 UN.
        lines[2] = lines[2] with { Product = feijao, Factor = 10, MatchedBy = NfeMatch.Manual };
        lines[3] = lines[3] with { Ignored = true };

        await Get<NfeImport>().ReceiveAsync(plan with { Lines = lines }, "gerente");

        var again = await PlanAsync();
        Assert.Equal(NfeMatch.Mapping, again.Lines[2].MatchedBy);
        Assert.Equal(10, again.Lines[2].Factor);
        Assert.Equal(feijao.Id, again.Lines[2].Product!.Id);
    }
}
