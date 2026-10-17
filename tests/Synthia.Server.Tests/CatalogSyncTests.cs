using System.Net.Http.Json;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Sync;

namespace Synthia.Server.Tests;

public sealed class CatalogSyncTests(ServerFixture fx) : IClassFixture<ServerFixture>
{
    private readonly HttpClient _http = fx.ApiClient();

    private static ProductData Product(string name, long cents) =>
        new(Guid.CreateVersion7(), name, null, null, "UN", cents, "00000000", true);

    private async Task<long> PutAsync(ProductData p)
    {
        var res = await _http.PutAsJsonAsync($"/backoffice/products/{p.Id}", p);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<VersionResponse>())!.Version;
    }

    [Fact]
    public async Task Caixa_recebe_so_o_delta_depois_da_sua_versao()
    {
        var v1 = await PutAsync(Product("Leite", 549));
        var cafe = Product("Café", 1899);
        var v2 = await PutAsync(cafe);
        var v3 = await PutAsync(cafe with { PriceCents = 1799 });

        var delta = await _http.GetFromJsonAsync<CatalogDeltaResponse>($"{SyncRoutes.Catalog}?since={v1}");

        Assert.False(delta!.SnapshotRequired);
        Assert.Equal([v2, v3], delta.Changes.Select(c => c.Version));
        Assert.Equal(v3, delta.LatestVersion);

        var empty = await _http.GetFromJsonAsync<CatalogDeltaResponse>($"{SyncRoutes.Catalog}?since={v3}");
        Assert.Empty(empty!.Changes);
    }

    [Fact]
    public async Task Caixa_novo_ou_a_frente_do_servidor_recebe_pedido_de_snapshot()
    {
        await PutAsync(Product("Pão", 99));

        var fresh = await _http.GetFromJsonAsync<CatalogDeltaResponse>($"{SyncRoutes.Catalog}?since=0");
        var ahead = await _http.GetFromJsonAsync<CatalogDeltaResponse>($"{SyncRoutes.Catalog}?since=999999");

        Assert.True(fresh!.SnapshotRequired);
        Assert.True(ahead!.SnapshotRequired);
    }

    [Fact]
    public async Task Snapshot_traz_precos_agendados_ainda_validos()
    {
        var p = Product("Açúcar", 499);
        await PutAsync(p);
        var price = new ScheduledPriceData(Guid.CreateVersion7(), p.Id, 459, DateTimeOffset.UtcNow.AddHours(1));
        (await _http.PostAsJsonAsync("/backoffice/scheduled-prices", price)).EnsureSuccessStatusCode();

        var snapshot = await _http.GetFromJsonAsync<CatalogSnapshotResponse>(SyncRoutes.CatalogSnapshot);

        Assert.Contains(snapshot!.Products, x => x.Id == p.Id);
        Assert.Contains(snapshot.ScheduledPrices, x => x.Id == price.Id && x.PriceCents == 459);
    }

    private sealed record VersionResponse(long Version);
}
