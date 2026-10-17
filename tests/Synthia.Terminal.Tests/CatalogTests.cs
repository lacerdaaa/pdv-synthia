using Synthia.Contracts;
using Synthia.Contracts.Catalog;

namespace Synthia.Terminal.Tests;

public sealed class CatalogTests : IDisposable
{
    private readonly TestTerminal _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public void Preco_agendado_vale_a_partir_do_horario_mesmo_offline()
    {
        var p = _t.SeedProduct("Café", 1899, barcode: "cafe");
        var price = new ScheduledPriceData(Guid.CreateVersion7(), p.Id, 1599, _t.Clock.GetUtcNow().AddHours(1));
        _t.Catalog.ApplyDelta([new CatalogChange(2, CatalogEntities.ScheduledPrice, price.Id, SynthiaJson.ToElement(price))]);

        Assert.Equal(1899, _t.Catalog.FindByBarcode("cafe")!.PriceCents);
        _t.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(1599, _t.Catalog.FindByBarcode("cafe")!.PriceCents);
    }

    [Fact]
    public void Edicao_direta_posterior_ao_agendamento_prevalece()
    {
        var p = _t.SeedProduct("Café", 1899, barcode: "cafe");
        var price = new ScheduledPriceData(Guid.CreateVersion7(), p.Id, 1599, _t.Clock.GetUtcNow().AddMinutes(-1));
        _t.Catalog.ApplyDelta([
            new CatalogChange(2, CatalogEntities.ScheduledPrice, price.Id, SynthiaJson.ToElement(price)),
            new CatalogChange(3, CatalogEntities.Product, p.Id, SynthiaJson.ToElement(p with { PriceCents = 1999 })),
        ]);

        Assert.Equal(1999, _t.Catalog.FindByBarcode("cafe")!.PriceCents);
    }

    [Fact]
    public void Produto_excluido_some_da_busca_mas_continua_por_id()
    {
        var p = _t.SeedProduct("Biscoito", 450, barcode: "bisc");
        _t.Catalog.ApplyDelta([new CatalogChange(2, CatalogEntities.Product, p.Id, SynthiaJson.ToElement(p with { Active = false }))]);

        Assert.Null(_t.Catalog.FindByBarcode("bisc"));
        Assert.NotNull(_t.Catalog.FindById(p.Id));
    }

    [Fact]
    public void Reaplicar_delta_antigo_nao_volta_o_catalogo()
    {
        var p = _t.SeedProduct("Leite", 549, barcode: "leite");
        var newer = new CatalogChange(2, CatalogEntities.Product, p.Id, SynthiaJson.ToElement(p with { PriceCents = 599 }));
        _t.Catalog.ApplyDelta([newer]);

        _t.Catalog.ApplyDelta([new CatalogChange(1, CatalogEntities.Product, p.Id, SynthiaJson.ToElement(p))]);

        Assert.Equal(599, _t.Catalog.FindByBarcode("leite")!.PriceCents);
        Assert.Equal(2, _t.Catalog.CurrentVersion());
    }

    [Fact]
    public async Task Puller_baixa_delta_e_snapshot_quando_pedido()
    {
        var a = new ProductData(Guid.CreateVersion7(), "A", "a", null, "UN", 100, "0", true);
        var b = new ProductData(Guid.CreateVersion7(), "B", "b", null, "UN", 200, "0", true);
        _t.Server.Changes.Add(new CatalogChange(1, CatalogEntities.Product, a.Id, SynthiaJson.ToElement(a)));

        Assert.Equal(1, await _t.Puller.PullAsync()); // caixa novo → snapshot
        Assert.NotNull(_t.Catalog.FindByBarcode("a"));

        _t.Server.Changes.Add(new CatalogChange(2, CatalogEntities.Product, b.Id, SynthiaJson.ToElement(b)));
        Assert.Equal(2, await _t.Puller.PullAsync()); // delta
        Assert.NotNull(_t.Catalog.FindByBarcode("b"));
    }

    [Fact]
    public async Task Servidor_fora_do_ar_congela_catalogo_na_ultima_versao()
    {
        _t.SeedProduct("Pão", 99, barcode: "1");
        _t.Server.Online = false;

        await Assert.ThrowsAsync<HttpRequestException>(() => _t.Puller.PullAsync());
        Assert.NotNull(_t.Catalog.FindByBarcode("1"));
        Assert.Equal(1, _t.Catalog.CurrentVersion());
    }
}
