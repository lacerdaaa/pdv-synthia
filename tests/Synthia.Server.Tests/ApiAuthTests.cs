using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Synthia.Contracts.Events;
using Synthia.Contracts.Sync;
using static Synthia.Server.Tests.EventBuilder;

namespace Synthia.Server.Tests;

public sealed class ApiAuthTests(ServerFixture fx) : IClassFixture<ServerFixture>
{
    [Fact]
    public async Task Api_do_backoffice_sem_credencial_responde_401_sem_redirecionar()
    {
        var http = fx.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/backoffice/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync("/backoffice/projections/rebuild", null)).StatusCode);
    }

    [Fact]
    public async Task Chave_de_api_errada_e_recusada_e_a_certa_aceita()
    {
        var wrong = fx.Factory.CreateClient();
        wrong.DefaultRequestHeaders.Add("X-Api-Key", "chute");

        Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.GetAsync("/backoffice/products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fx.ApiClient().GetAsync("/backoffice/products")).StatusCode);
    }

    [Fact]
    public async Task Documentacao_e_saude_continuam_publicas()
    {
        var http = fx.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/docs/")).StatusCode);
    }

    /// <summary>Com chaves configuradas, cada caixa precisa da sua e só envia os próprios eventos.</summary>
    [Fact]
    public async Task Sync_exige_chave_do_caixa_e_recusa_eventos_de_outro_caixa()
    {
        await using var factory = fx.Factory.WithWebHostBuilder(b => b
            .UseSetting("Sync:Terminals:CX01", "chave-cx01")
            .UseSetting("Sync:Terminals:CX02", "chave-cx02"));
        HttpClient Caixa(string id, string key)
        {
            var http = factory.CreateClient();
            http.DefaultRequestHeaders.Add("X-Terminal-Id", id);
            http.DefaultRequestHeaders.Add("X-Terminal-Key", key);
            return http;
        }
        var (sale, _) = Sale(Guid.CreateVersion7(), (Guid.CreateVersion7(), 1, 100)); // TerminalId = CX01

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"{SyncRoutes.Catalog}?since=0")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Caixa("CX01", "errada").GetAsync($"{SyncRoutes.Catalog}?since=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Caixa("CX01", "chave-cx01").GetAsync($"{SyncRoutes.Catalog}?since=0")).StatusCode);

        var forged = await Caixa("CX02", "chave-cx02").PostAsJsonAsync(SyncRoutes.Events, new PushEventsRequest([sale]));
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);

        var own = await Caixa("CX01", "chave-cx01").PostAsJsonAsync(SyncRoutes.Events, new PushEventsRequest([sale]));
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task Fora_de_desenvolvimento_sync_sem_chaves_configuradas_e_recusado()
    {
        await using var factory = fx.Factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));

        var res = await factory.CreateClient().GetAsync($"{SyncRoutes.Catalog}?since=0");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
