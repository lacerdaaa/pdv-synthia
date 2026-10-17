using System.Data;
using Dapper;
using Synthia.Contracts;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Sync;
using Synthia.Terminal.Core.Storage;

namespace Synthia.Terminal.Core.Catalog;

/// <summary>Produto pronto para a venda, com o preço vigente agora.</summary>
public sealed record CatalogItem(Guid Id, string Name, string? Barcode, int? Plu, string Unit, long PriceCents, string Ncm);

/// <summary>Cópia local do catálogo. Leitura para a venda; escrita só pelo sync com o servidor.</summary>
public sealed class LocalCatalog(LocalDb db, TimeProvider clock)
{
    // Preço vigente: o agendamento mais recente já em vigor, desde que posterior à última
    // edição direta do produto; senão, o preço do cadastro.
    private const string SelectItem = """
        SELECT p.id AS Id, p.name AS Name, p.barcode AS Barcode, p.plu AS Plu, p.unit AS Unit, p.ncm AS Ncm,
               coalesce((SELECT sp.price_cents FROM scheduled_prices sp
                         WHERE sp.product_id = p.id AND sp.version > p.version AND sp.effective_at <= @now
                         ORDER BY sp.effective_at DESC, sp.version DESC LIMIT 1), p.price_cents) AS PriceCents
        FROM products p
        """;

    public CatalogItem? FindByBarcode(string barcode) => Find("p.barcode = @barcode AND p.active = 1", new { barcode });

    public CatalogItem? FindByPlu(int plu) => Find("p.plu = @plu AND p.active = 1", new { plu });

    /// <summary>Inclui inativos: um produto excluído no back-office ainda pode estar numa venda offline.</summary>
    public CatalogItem? FindById(Guid id) => Find("p.id = @id", new { id });

    public long CurrentVersion()
    {
        using var conn = db.Open();
        return Meta.GetCatalogVersion(conn);
    }

    private CatalogItem? Find(string where, object args)
    {
        using var conn = db.Open();
        var p = new DynamicParameters(args);
        p.Add("now", LocalDb.ToDbTime(clock.GetUtcNow()));
        return conn.QuerySingleOrDefault<ItemRow>($"{SelectItem} WHERE {where} LIMIT 1", p)?.ToItem();
    }

    // SQLite devolve TEXT/INTEGER; a classe com setters deixa o Dapper converter os inteiros.
    // Guid é lido como texto e convertido aqui, sem type handler global do Dapper (que
    // afetaria também o Npgsql quando os dois rodam no mesmo processo).
    private sealed class ItemRow
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Barcode { get; init; }
        public int? Plu { get; init; }
        public string Unit { get; init; } = "";
        public long PriceCents { get; init; }
        public string Ncm { get; init; } = "";

        public CatalogItem ToItem() => new(Guid.Parse(Id), Name, Barcode, Plu, Unit, PriceCents, Ncm);
    }

    /// <summary>Upsert idempotente: reaplicar o mesmo delta não muda nada.</summary>
    public void ApplyDelta(IReadOnlyList<CatalogChange> changes)
    {
        if (changes.Count == 0) return;

        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        var current = Meta.GetCatalogVersion(conn, tx);
        foreach (var change in changes.Where(c => c.Version > current).OrderBy(c => c.Version))
        {
            switch (change.Entity)
            {
                case CatalogEntities.Product:
                    UpsertProduct(conn, tx, SynthiaJson.FromElement<ProductData>(change.Data), change.Version);
                    break;
                case CatalogEntities.ScheduledPrice:
                    UpsertScheduledPrice(conn, tx, SynthiaJson.FromElement<ScheduledPriceData>(change.Data), change.Version);
                    break;
                // Entidades desconhecidas (versão nova do servidor) são puladas sem travar o sync.
            }
            current = change.Version;
        }

        Meta.Set(conn, Meta.LastCatalogVersion, current.ToString(), tx);
        tx.Commit();
    }

    /// <summary>Substitui todo o catálogo local (caixa novo, SQLite corrompido ou muito atrasado).</summary>
    public void ApplySnapshot(CatalogSnapshotResponse snapshot)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        conn.Execute("DELETE FROM scheduled_prices; DELETE FROM products;", transaction: tx);
        // Num snapshot todos os itens são "da mesma versão"; usar version - 1 para produtos
        // mantém os agendamentos (version) como posteriores ao cadastro.
        foreach (var product in snapshot.Products)
            UpsertProduct(conn, tx, product, snapshot.Version - 1);
        foreach (var price in snapshot.ScheduledPrices)
            UpsertScheduledPrice(conn, tx, price, snapshot.Version);

        Meta.Set(conn, Meta.LastCatalogVersion, snapshot.Version.ToString(), tx);
        tx.Commit();
    }

    private static void UpsertProduct(IDbConnection conn, IDbTransaction tx, ProductData p, long version) =>
        conn.Execute(
            """
            INSERT INTO products (id, name, barcode, plu, unit, price_cents, ncm, active, version)
            VALUES (@Id, @Name, @Barcode, @Plu, @Unit, @PriceCents, @Ncm, @Active, @version)
            ON CONFLICT (id) DO UPDATE SET
              name = excluded.name, barcode = excluded.barcode, plu = excluded.plu, unit = excluded.unit,
              price_cents = excluded.price_cents, ncm = excluded.ncm, active = excluded.active, version = excluded.version
            """,
            new { p.Id, p.Name, p.Barcode, p.Plu, p.Unit, p.PriceCents, p.Ncm, Active = p.Active ? 1 : 0, version },
            tx);

    private static void UpsertScheduledPrice(IDbConnection conn, IDbTransaction tx, ScheduledPriceData s, long version) =>
        conn.Execute(
            """
            INSERT INTO scheduled_prices (id, product_id, price_cents, effective_at, version)
            VALUES (@Id, @ProductId, @PriceCents, @EffectiveAt, @version)
            ON CONFLICT (id) DO UPDATE SET
              price_cents = excluded.price_cents, effective_at = excluded.effective_at, version = excluded.version
            """,
            new { s.Id, s.ProductId, s.PriceCents, EffectiveAt = LocalDb.ToDbTime(s.EffectiveAt), version },
            tx);
}
