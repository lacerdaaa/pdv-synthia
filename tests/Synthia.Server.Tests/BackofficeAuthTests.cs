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

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var http = Client();
        var page = await http.GetStringAsync("/retaguarda/login");
        var res = await http.PostAsync("/retaguarda/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["__RequestVerificationToken"] = AntiforgeryRegex().Match(page).Groups[1].Value,
            ["Input.Username"] = username,
            ["Input.Password"] = password,
        }));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        return http;
    }

    [Fact]
    public async Task Estoquista_nao_acessa_produtos_mas_acessa_estoque()
    {
        var users = fx.Factory.Services.GetRequiredService<BackofficeUsers>();
        var u = await users.CreateAsync($"est{Guid.NewGuid():N}"[..12], "Estoquista", "segredo3", BackofficeRoles.Stock);
        var http = await LoginAsync(u.Username, "segredo3");

        var produtos = await http.GetAsync("/retaguarda/produtos");
        Assert.Equal(HttpStatusCode.Redirect, produtos.StatusCode);
        Assert.Equal("/retaguarda/sem-permissao", produtos.Headers.Location!.AbsolutePath);

        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/retaguarda/estoque")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/retaguarda/inventarios")).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await http.GetAsync("/retaguarda/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Usuario_desativado_perde_o_acesso_na_hora()
    {
        var users = fx.Factory.Services.GetRequiredService<BackofficeUsers>();
        var u = await UserAsync("segredo4");
        var http = await LoginAsync(u.Username, "segredo4");
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/retaguarda")).StatusCode);

        await users.SetActiveAsync(u.Id, false);

        var res = await http.GetAsync("/retaguarda");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.StartsWith("/retaguarda/login", res.Headers.Location!.AbsolutePath);
        Assert.Null(await users.VerifyAsync(u.Username, "segredo4"));
    }

    [Fact]
    public async Task Troca_da_propria_senha_confere_a_atual()
    {
        var users = fx.Factory.Services.GetRequiredService<BackofficeUsers>();
        var u = await UserAsync("antiga1");

        await Assert.ThrowsAsync<ArgumentException>(() => users.ChangeOwnPasswordAsync(u.Id, "errada", "nova123"));
        await users.ChangeOwnPasswordAsync(u.Id, "antiga1", "nova123");

        Assert.Null(await users.VerifyAsync(u.Username, "antiga1"));
        Assert.NotNull(await users.VerifyAsync(u.Username, "nova123"));
    }

    [Fact]
    public async Task Nao_fica_sem_administrador_ativo()
    {
        var users = fx.Factory.Services.GetRequiredService<BackofficeUsers>();
        // Neste banco o único admin é o criado no primeiro start.
        var admin = (await users.ListAsync()).Single(x => x.Role == BackofficeRoles.Admin);

        await Assert.ThrowsAsync<ArgumentException>(() => users.SetActiveAsync(admin.Id, false));
        await Assert.ThrowsAsync<ArgumentException>(() => users.SetRoleAsync(admin.Id, BackofficeRoles.Stock));
        Assert.True(await users.IsActiveAsync(admin.Id));

        var u = await UserAsync("segredo5");
        await Assert.ThrowsAsync<ArgumentException>(() => users.SetRoleAsync(u.Id, "dono"));
        await Assert.ThrowsAsync<ArgumentException>(() => users.CreateAsync(u.Username, "Duplicado", "segredo5"));
    }
}
