using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Synthia.Server.Backoffice;

namespace Synthia.Server.Tests;

public sealed partial class BackofficeAuthTests(ServerFixture fx) : IClassFixture<ServerFixture>
{
    private HttpClient Client() => fx.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private async Task<BackofficeUser> UserAsync(string password)
    {
        var users = fx.Factory.Services.GetRequiredService<BackofficeUsers>();
        return await users.CreateAsync($"gerente{Guid.NewGuid():N}"[..14], "Gerente Teste", password);
    }

    [Theory]
    [InlineData("/retaguarda")]
    [InlineData("/retaguarda/produtos")]
    [InlineData("/retaguarda/estoque")]
    public async Task Paginas_exigem_login(string path)
    {
        var res = await Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.StartsWith("/retaguarda/login", res.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Senha_e_verificada_pelo_hash()
    {
        var users = fx.Factory.Services.GetRequiredService<BackofficeUsers>();
        var user = await UserAsync("segredo1");

        Assert.NotNull(await users.VerifyAsync(user.Username.ToUpperInvariant(), "segredo1"));
        Assert.Null(await users.VerifyAsync(user.Username, "errada"));
        await Assert.ThrowsAsync<ArgumentException>(() => users.CreateAsync("x", "x", "123"));
    }

    [Fact]
    public async Task Login_pelo_formulario_abre_a_retaguarda()
    {
        var user = await UserAsync("segredo2");
        var http = Client();

        var page = await http.GetStringAsync("/retaguarda/login");
        var token = AntiforgeryRegex().Match(page).Groups[1].Value;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["__RequestVerificationToken"] = token,
            ["Input.Username"] = user.Username,
            ["Input.Password"] = "segredo2",
        });

        var login = await http.PostAsync("/retaguarda/login", form);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/retaguarda", login.Headers.Location!.AbsolutePath);

        var painel = await http.GetAsync("/retaguarda");
        Assert.Equal(HttpStatusCode.OK, painel.StatusCode);
        Assert.Contains("Implantação do mercado novo", WebUtility.HtmlDecode(await painel.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Login_invalido_continua_na_tela_de_login()
    {
        var http = Client();
        var page = await http.GetStringAsync("/retaguarda/login");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["__RequestVerificationToken"] = AntiforgeryRegex().Match(page).Groups[1].Value,
            ["Input.Username"] = "ninguem",
            ["Input.Password"] = "nada",
        });

        var res = await http.PostAsync("/retaguarda/login", form);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("Usuário ou senha inválidos", WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("/retaguarda/modelos/produtos.csv", "codigo_barras;plu;descricao")]
    [InlineData("/retaguarda/modelos/entrada.csv", "codigo;quantidade")]
    public async Task Modelos_de_planilha_tem_BOM_para_o_Excel(string path, string header)
    {
        var bytes = await Client().GetByteArrayAsync(path);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.StartsWith(header, System.Text.Encoding.UTF8.GetString(bytes[3..]));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryRegex();
}
