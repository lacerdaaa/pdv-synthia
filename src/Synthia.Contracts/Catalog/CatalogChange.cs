using System.Text.Json;

namespace Synthia.Contracts.Catalog;

/// <summary>
/// Uma linha de <c>catalog_changes</c>. O servidor é o único que atribui <see cref="Version"/>,
/// um contador global e sequencial.
/// </summary>
public sealed record CatalogChange(long Version, string Entity, Guid EntityId, JsonElement Data);

public static class CatalogEntities
{
    /// <summary>Cadastro completo do produto (upsert). Exclusão é lógica: <c>Active = false</c>.</summary>
    public const string Product = "product";

    /// <summary>Preço agendado; o caixa aplica sozinho quando chega <c>EffectiveAt</c>.</summary>
    public const string ScheduledPrice = "scheduled_price";
}

public sealed record ProductData(
    Guid Id,
    string Name,
    string? Barcode,
    int? Plu,
    string Unit,
    long PriceCents,
    string Ncm,
    bool Active);

public sealed record ScheduledPriceData(Guid Id, Guid ProductId, long PriceCents, DateTimeOffset EffectiveAt);
