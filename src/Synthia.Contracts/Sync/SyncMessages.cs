using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;

namespace Synthia.Contracts.Sync;

public static class SyncRoutes
{
    public const string Events = "/sync/events";
    public const string Catalog = "/sync/catalog";
    public const string CatalogSnapshot = "/sync/catalog/snapshot";
}

public sealed record PushEventsRequest(IReadOnlyList<EventEnvelope> Events);

/// <summary>IDs gravados pelo servidor, incluindo os que já existiam (reenvio é inofensivo).</summary>
public sealed record PushEventsResponse(IReadOnlyList<Guid> Accepted);

/// <param name="SnapshotRequired">
/// Verdadeiro quando o caixa ficou para trás demais; ele deve baixar o snapshot completo.
/// </param>
public sealed record CatalogDeltaResponse(
    long Since,
    long LatestVersion,
    bool SnapshotRequired,
    IReadOnlyList<CatalogChange> Changes);

public sealed record CatalogSnapshotResponse(
    long Version,
    IReadOnlyList<ProductData> Products,
    IReadOnlyList<ScheduledPriceData> ScheduledPrices);
