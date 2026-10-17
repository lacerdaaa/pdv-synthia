using Synthia.Terminal.Core.Catalog;
using Synthia.Terminal.Core.Sales;

namespace Synthia.Terminal.Tests;

public sealed class ScaleBarcodeTests : IDisposable
{
    private readonly TestTerminal _t = new();

    public void Dispose() => _t.Dispose();

    [Theory]
    [InlineData("7891000100103", true)]
    [InlineData("7891000100104", false)]
    [InlineData("789100010010", false)]
    [InlineData("78910001001AB", false)]
    public void Valida_digito_verificador_do_EAN13(string code, bool valid) =>
        Assert.Equal(valid, Ean13.IsValid(code));

    [Fact]
    public void Etiqueta_padrao_traz_plu_de_6_digitos_e_total_em_centavos()
    {
        Assert.True(ScaleBarcode.TryParse("2001001007409", ScaleBarcodeFormat.Default, out var label));

        Assert.Equal(1001, label.Plu);
        Assert.Equal(740, label.TotalCents);
        Assert.Null(label.WeightKg);
    }

    [Fact]
    public void Etiqueta_configurada_para_peso_traz_gramas()
    {
        var format = new ScaleBarcodeFormat(Value: ScaleLabelValue.Weight);

        Assert.True(ScaleBarcode.TryParse("2001001012359", format, out var label));

        Assert.Equal(1.235m, label.WeightKg);
        Assert.Null(label.TotalCents);
    }

    [Theory]
    [InlineData("7891000100103")] // não começa com 2
    [InlineData("2001001007408")] // dígito verificador errado
    public void Rejeita_o_que_nao_e_etiqueta_valida(string code) =>
        Assert.False(ScaleBarcode.TryParse(code, ScaleBarcodeFormat.Default, out _));

    [Fact]
    public void Formato_precisa_fechar_13_digitos() =>
        Assert.Throws<ArgumentException>(() => new ScaleBarcodeFormat(CodeLength: 4, ValueLength: 5).Validate());

    [Fact]
    public void Leitura_de_etiqueta_cobra_o_total_impresso()
    {
        _t.SeedProduct("Banana prata", 599, plu: 1001);
        var cart = new Cart();

        var item = new ScanResolver(_t.Catalog, ScaleBarcodeFormat.Default).AddTo(cart, "2001001007409");

        Assert.NotNull(item);
        Assert.Equal(740, item.TotalCents);
        Assert.Equal(1.235m, item.Quantity); // 740 / 599 = 1,2354 → 1,235 kg para o estoque
    }

    [Fact]
    public void Etiqueta_por_peso_calcula_pelo_preco_do_caixa()
    {
        _t.SeedProduct("Banana prata", 599, plu: 1001);
        var resolver = new ScanResolver(_t.Catalog, new ScaleBarcodeFormat(Value: ScaleLabelValue.Weight));

        var item = resolver.AddTo(new Cart(), "2001001012359");

        Assert.Equal(740, item!.TotalCents);
    }

    [Fact]
    public void Codigo_exato_do_cadastro_tem_prioridade_sobre_etiqueta()
    {
        _t.SeedProduct("Kit interno", 1500, barcode: "2009999005001");

        var r = new ScanResolver(_t.Catalog, ScaleBarcodeFormat.Default).Resolve("2009999005001");

        Assert.Equal("Kit interno", r!.Product.Name);
        Assert.Null(r.FixedTotalCents);
    }

    [Fact]
    public void Plu_inexistente_nao_entra_no_carrinho()
    {
        var cart = new Cart();

        Assert.Null(new ScanResolver(_t.Catalog, ScaleBarcodeFormat.Default).AddTo(cart, "2001001007409"));
        Assert.Empty(cart.Items);
    }
}
