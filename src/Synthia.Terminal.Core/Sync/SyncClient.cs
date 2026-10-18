using System.Net.Http.Json;
using Synthia.Contracts;
using Synthia.Contracts.Events;
using Synthia.Contracts.Sync;

namespace Synthia.Terminal.Core.Sync;

/// <summary>HTTP com o servidor da loja. Configure o <see cref="HttpClient"/> com BaseAddress e timeout curto.</summary>
public sealed class SyncClient(HttpClient http)
{
    public async Task<IReadOnlyList<Guid>> PushEventsAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct)
    {
        using var res = await http.PostAsJsonAsync(SyncRoutes.Events, new PushEventsRequest(events), SynthiaJson.Options, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PushEventsResponse>(SynthiaJson.Options, ct);
        return body?.Accepted ?? [];
    }

    public async Task<CatalogDeltaResponse> GetCatalogDeltaAsync(long since, CancellationToken ct) =>
        await http.GetFromJsonAsync<CatalogDeltaResponse>($"{SyncRoutes.Catalog}?since={since}", SynthiaJson.Options, ct)
        ?? throw new InvalidOperationException("Resposta vazia do catálogo.");

    public async Task<StoreProfile> GetStoreProfileAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<StoreProfile>(SyncRoutes.Store, SynthiaJson.Options, ct)
        ?? throw new InvalidOperationException("Resposta vazia dos dados da loja.");

    /// <summary>Bytes da logo, ou nulo quando a loja não cadastrou uma.</summary>
    public async Task<byte[]?> GetStoreLogoAsync(CancellationToken ct)
    {
        using var res = await http.GetAsync(SyncRoutes.StoreLogo, ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<CatalogSnapshotResponse> GetCatalogSnapshotAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<CatalogSnapshotResponse>(SyncRoutes.CatalogSnapshot, SynthiaJson.Options, ct)
        ?? throw new InvalidOperationException("Resposta vazia do snapshot.");
}
