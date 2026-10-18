using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Synthia.Contracts;
using Synthia.Contracts.Sync;
using Synthia.Server.Backoffice;

namespace Synthia.Server.Tests;

public sealed class StoreProfileTests(ServerFixture fx) : IClassFixture<ServerFixture>
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private StoreProfileService Service() => fx.Factory.Services.GetRequiredService<StoreProfileService>();

    private static StoreProfileInput Input(string name = "Mercado Teste", string cnpj = "11.222.333/0001-81") =>
        new(name, "Mercado Teste Ltda", cnpj, "123.456.789.110", "Rua A, 10 - Centro", "Obrigado pela preferência!", "#253c70", "#C8272E");

    [Fact]
    public async Task Loja_nova_tem_identificador_e_nome_padrao()
    {
        var profile = await Service().GetAsync();

        Assert.NotEqual(Guid.Empty, profile.StoreId);
        Assert.False(string.IsNullOrWhiteSpace(profile.TradeName));
        Assert.True(profile.Version >= 1);
    }

    [Fact]
    public async Task Alteracao_grava_normalizada_e_sobe_a_versao()
    {
        var before = await Service().GetAsync();

        var after = await Service().UpdateAsync(Input());

        Assert.Equal(before.StoreId, after.StoreId);
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal("11222333000181", after.Cnpj);
        Assert.Equal("#253C70", after.PrimaryColor);
        Assert.Equal("Mercado Teste", (await Service().GetAsync()).TradeName);
    }

    [Theory]
    [InlineData("", "11.222.333/0001-81")]
    [InlineData("Mercado", "11.222.333/0001-82")]
    [InlineData("Um nome de loja comprido demais para caber no cupom", "")]
    public async Task Dados_invalidos_sao_recusados(string name, string cnpj)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().UpdateAsync(Input(name, cnpj)));
    }

    [Fact]
    public async Task Logo_aceita_png_recusa_outros_formatos_e_muda_o_hash()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().SetLogoAsync("GIF89a..."u8.ToArray()));

        var withLogo = await Service().SetLogoAsync(Png);
        Assert.NotNull(withLogo.LogoHash);
        Assert.Equal("image/png", (await Service().GetLogoAsync())!.ContentType);

        var without = await Service().RemoveLogoAsync();
        Assert.Null(without.LogoHash);
        Assert.Null(await Service().GetLogoAsync());
    }

    [Fact]
    public async Task Caixa_recebe_dados_e_logo_da_loja_pelo_sync()
    {
        await Service().UpdateAsync(Input("Mercado do Sync"));
        await Service().SetLogoAsync(Png);
        var http = fx.Factory.CreateClient();

        var profile = await http.GetFromJsonAsync<StoreProfile>(SyncRoutes.Store, SynthiaJson.Options);
        var logo = await http.GetAsync(SyncRoutes.StoreLogo);

        Assert.Equal("Mercado do Sync", profile!.TradeName);
        Assert.NotNull(profile.LogoHash);
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await logo.Content.ReadAsByteArrayAsync());
    }
}
