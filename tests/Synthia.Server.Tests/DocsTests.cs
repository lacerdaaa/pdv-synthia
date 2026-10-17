using System.Net;
using System.Text.Json;

namespace Synthia.Server.Tests;

public sealed class DocsTests(ServerFixture fx) : IClassFixture<ServerFixture>
{
    private readonly HttpClient _http = fx.Factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Raiz_redireciona_para_a_documentacao()
    {
        var res = await _http.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/docs/", res.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/docs/")]
    [InlineData("/docs/simulacao.html")]
    public async Task Documentacao_interativa_e_servida(string path)
    {
        var res = await _http.GetAsync(path);

        res.EnsureSuccessStatusCode();
        Assert.Equal("text/html", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApi_descreve_todas_as_rotas_de_sync_e_back_office()
    {
        using var doc = JsonDocument.Parse(await _http.GetStringAsync("/openapi/v1.json"));
        var operations = doc.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject())
            .Select(o => o.Value.GetProperty("operationId").GetString())
            .ToHashSet();

        Assert.Superset(
            new HashSet<string?> { "PushEvents", "GetCatalogDelta", "GetCatalogSnapshot", "ListProducts", "UpsertProduct", "SchedulePrice", "RebuildProjections" },
            operations);
    }
}
