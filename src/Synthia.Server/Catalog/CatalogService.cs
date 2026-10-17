using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using Synthia.Contracts;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Sync;

namespace Synthia.Server.Catalog;

/// <summary>
/// Escritas do back-office no catálogo. Cada escrita gera uma linha em <c>catalog_changes</c>
/// com <c>version</c> sequencial; é isso que os caixas puxam.
/// </summary>
public sealed class CatalogService(NpgsqlDataSource dataSource)
{
    public const int DeltaPageSize = 500;

    /// <summary>Acima dessa distância o caixa baixa o snapshot em vez de paginar o delta.</summary>
    public const long MaxDeltaDistance = 5000;

    public async Task<long> UpsertProductAsync(ProductData product, CancellationToken ct = default) =>
        await UpsertProductsAsync([product], ct);

    /// <summary>Grava vários produtos numa transação (importação de planilha). Devolve a última versão.</summary>
    public async Task<long> UpsertProductsAsync(IReadOnlyList<ProductData> products, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        long version = 0;
        foreach (var product in products)
        {
            version = await AppendChangeAsync(conn, tx, CatalogEntities.Product, product.Id, product);
            await conn.ExecuteAsync(
                """
                INSERT INTO products (id, name, barcode, plu, unit, price_cents, ncm, active, version)
                VALUES (@Id, @Name, @Barcode, @Plu, @Unit, @PriceCents, @Ncm, @Active, @version)
                ON CONFLICT (id) DO UPDATE SET
                  name = excluded.name, barcode = excluded.barcode, plu = excluded.plu, unit = excluded.unit,
                  price_cents = excluded.price_cents, ncm = excluded.ncm, active = excluded.active, version = excluded.version
                """,
                new { product.Id, product.Name, product.Barcode, product.Plu, product.Unit, product.PriceCents, product.Ncm, product.Active, version },
                tx);
        }

        await tx.CommitAsync(ct);
        return version;
    }

    public async Task<long> SchedulePriceAsync(ScheduledPriceData price, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var version = await AppendChangeAsync(conn, tx, CatalogEntities.ScheduledPrice, price.Id, price);
        await conn.ExecuteAsync(
            """
            INSERT INTO scheduled_prices (id, product_id, price_cents, effective_at, version)
            VALUES (@Id, @ProductId, @PriceCents, @EffectiveAt, @version)
            """,
            new { price.Id, price.ProductId, price.PriceCents, EffectiveAt = price.EffectiveAt.UtcDateTime, version },
            tx);

        await tx.CommitAsync(ct);
        return version;
    }

    public async Task<CatalogDeltaResponse> GetDeltaAsync(long since, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var latest = await conn.ExecuteScalarAsync<long>("SELECT coalesce(max(version), 0) FROM catalog_changes");

        // since > latest: o banco do servidor foi restaurado de um backup mais antigo que o caixa.
        if (since <= 0 && latest > 0 || since > latest || latest - since > MaxDeltaDistance)
            return new CatalogDeltaResponse(since, latest, SnapshotRequired: true, []);

        var rows = await conn.QueryAsync<(long Version, string Entity, Guid EntityId, string Data)>(
            "SELECT version, entity, entity_id, data::text FROM catalog_changes WHERE version > @since ORDER BY version LIMIT @limit",
            new { since, limit = DeltaPageSize });

        var changes = rows
            .Select(r => new CatalogChange(r.Version, r.Entity, r.EntityId, JsonDocument.Parse(r.Data).RootElement.Clone()))
            .ToList();

        return new CatalogDeltaResponse(since, latest, SnapshotRequired: false, changes);
    }

    public async Task<CatalogSnapshotResponse> GetSnapshotAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var version = await conn.ExecuteScalarAsync<long>("SELECT coalesce(max(version), 0) FROM catalog_changes", transaction: tx);
        var products = await ListProductsAsync(conn, tx);

        // Só os agendamentos posteriores à última edição direta do produto ainda valem.
        var prices = (await conn.QueryAsync<(Guid Id, Guid ProductId, long PriceCents, DateTime EffectiveAt)>(
                """
                SELECT sp.id, sp.product_id, sp.price_cents, sp.effective_at
                FROM scheduled_prices sp JOIN products p ON p.id = sp.product_id
                WHERE sp.version > p.version
                ORDER BY sp.effective_at
                """, transaction: tx))
            .Select(r => new ScheduledPriceData(r.Id, r.ProductId, r.PriceCents, new DateTimeOffset(DateTime.SpecifyKind(r.EffectiveAt, DateTimeKind.Utc))))
            .ToList();

        await tx.CommitAsync(ct);
        return new CatalogSnapshotResponse(version, products, prices);
    }

    public async Task<IReadOnlyList<ProductData>> ListProductsAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await ListProductsAsync(conn, null);
    }

    private static async Task<IReadOnlyList<ProductData>> ListProductsAsync(NpgsqlConnection conn, IDbTransaction? tx) =>
        (await conn.QueryAsync<ProductData>(
            "SELECT id, name, barcode, plu, unit, price_cents AS PriceCents, ncm, active FROM products ORDER BY name",
            transaction: tx)).ToList();

    private static async Task<long> AppendChangeAsync<T>(NpgsqlConnection conn, IDbTransaction tx, string entity, Guid entityId, T data)
    {
        // Serializa os escritores: sem isso, versões poderiam ser confirmadas fora de ordem e um
        // caixa que leu "since=N" perderia para sempre uma versão menor confirmada depois.
        await conn.ExecuteAsync("SELECT pg_advisory_xact_lock(7231002)", transaction: tx);

        return await conn.ExecuteScalarAsync<long>(
            "INSERT INTO catalog_changes (entity, entity_id, data) VALUES (@entity, @entityId, @data::jsonb) RETURNING version",
            new { entity, entityId, data = JsonSerializer.Serialize(data, SynthiaJson.Options) },
            tx);
    }
}
