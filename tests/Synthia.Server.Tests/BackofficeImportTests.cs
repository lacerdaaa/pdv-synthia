using System.Text;
using Synthia.Contracts.Catalog;
using Synthia.Server.Backoffice;

namespace Synthia.Server.Tests;

public sealed class BackofficeImportTests
{
    private static readonly ProductData Arroz = new(Guid.NewGuid(), "Arroz 5kg", "7891000100103", null, "UN", 2899, "10063021", true);
    private static readonly ProductData Banana = new(Guid.NewGuid(), "Banana", null, 1001, "KG", 599, "08039000", true);

    [Fact]
    public void Modelo_de_planilha_importa_sem_erros()
    {
        var plan = ProductImport.Plan(ProductImport.Template, []);

        Assert.All(plan, r => Assert.Equal(ImportAction.Create, r.Action));
        Assert.Equal("KG", plan[1].Product!.Unit);
        Assert.Equal(599, plan[1].Product!.PriceCents);
    }

    [Fact]
    public void Produto_existente_e_atualizado_pelo_codigo_de_barras_ou_plu()
    {
        const string csv = "Código de Barras;PLU;Descrição;Preço;NCM\n7891000100103;;Arroz 5kg;29,49;10063021\n;1001;Banana;5,99;08039000\n";

        var plan = ProductImport.Plan(csv, [Arroz, Banana]);

        Assert.Equal(ImportAction.Update, plan[0].Action);
        Assert.Equal(Arroz.Id, plan[0].Product!.Id);
        Assert.Equal(2949, plan[0].Product!.PriceCents);
        Assert.Equal(ImportAction.Unchanged, plan[1].Action);
    }

    [Fact]
    public void Erros_e_avisos_por_linha()
    {
        const string csv = "codigo_barras;descricao;preco;ncm\n123;;abc;\n7891000100103;Arroz;10;10063021\n7891000100103;Arroz de novo;10;10063021\n7891000100104;Código errado;1;1\n";

        var plan = ProductImport.Plan(csv, []);

        Assert.Equal(ImportAction.Error, plan[0].Action);
        Assert.Contains(plan[0].Errors, e => e.Contains("descrição"));
        Assert.Contains(plan[0].Errors, e => e.Contains("preço"));
        Assert.Equal(ImportAction.Create, plan[1].Action);
        Assert.Contains(plan[2].Errors, e => e.Contains("repetido"));
        Assert.Equal(ImportAction.Create, plan[3].Action);
        Assert.Equal(2, plan[3].Warnings.Count); // dígito verificador e NCM
    }

    [Fact]
    public void Cabecalho_sem_colunas_obrigatorias()
    {
        var plan = ProductImport.Plan("nome;valor_x\nArroz;1\n", []);

        Assert.Equal(ImportAction.Error, Assert.Single(plan).Action);
    }

    [Fact]
    public void Planilha_do_Excel_em_Windows_1252_e_virgula()
    {
        var bytes = Encoding.GetEncoding(1252).GetBytes("codigo_barras,descricao,preco\n7891000100103,Feijão,\"1.234,50\"\n");

        var plan = ProductImport.Plan(CsvText.Decode(bytes), []);

        Assert.Equal("Feijão", plan[0].Product!.Name);
        Assert.Equal(123450, plan[0].Product!.PriceCents);
    }

    [Theory]
    [InlineData("28,99", 2899)]
    [InlineData("1.234,56", 123456)]
    [InlineData("1234.56", 123456)]
    [InlineData("R$ 5", 500)]
    public void Valores_em_formatos_brasileiros(string text, long cents)
    {
        Assert.True(CsvText.TryMoney(text, out var parsed));
        Assert.Equal(cents, parsed);
    }

    [Fact]
    public void Entrada_por_planilha_soma_repetidos_e_aponta_erros()
    {
        const string csv = "codigo;quantidade;custo\n7891000100103;24;20,50\n1001;12,5;\n7891000100103;6;\n999;1;\n7891000100103;1,5;\n";

        var plan = ReceiptImport.Plan(csv, [Arroz, Banana]);

        var arroz = plan.Single(r => r.Item?.ProductId == Arroz.Id).Item!;
        Assert.Equal(30, arroz.Quantity);
        Assert.Equal(2050, arroz.UnitCostCents);
        Assert.Equal(12.5m, plan.Single(r => r.Item?.ProductId == Banana.Id).Item!.Quantity);
        Assert.Contains(plan, r => r.Error?.Contains("não encontrado") == true);
        Assert.Contains(plan, r => r.Error?.Contains("fracionada") == true);
    }

    [Theory]
    [InlineData("7891000100103", true)]
    [InlineData("7891000100104", false)]
    [InlineData("96385074", true)]
    [InlineData("123", false)]
    public void Digito_verificador_GTIN(string code, bool valid) => Assert.Equal(valid, Gtin.IsValid(code));
}
