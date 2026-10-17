using Dapper;
using Npgsql;
using Synthia.Contracts.Events;

namespace Synthia.Server.Backoffice;

public sealed record Inventory(Guid Id, string Name, bool FullCount, string Status, string CreatedBy, DateTime CreatedAt, string? ClosedBy, DateTime? ClosedAt, int Lines);

public sealed record InventoryLine(Guid ProductId, string Name, string? Barcode, int? Plu, string Unit, decimal Quantity, DateTime CountedAt, string CountedBy);

/// <param name="Expected">Saldo do sistema agora.</param>
/// <param name="Counted">Contado; em inventário geral, 0 para o que não foi contado.</param>
public sealed record InventoryReviewLine(Guid ProductId, string Name, string Unit, decimal Expected, decimal Counted, bool WasCounted)
{
    public decimal Difference => Counted - Expected;
}

/// <summary>
/// Inventário (contagem física) da retaguarda. Por setor (rotativo) ou geral. Enquanto aberto é só
/// rascunho; ao confirmar vira um <see cref="StockCounted"/> por produto, com o horário em que cada
/// item foi contado, e o saldo passa a ser a contagem + movimentos posteriores.
/// </summary>
public sealed class InventoryService(NpgsqlDataSource dataSource, BackofficeEvents events, TimeProvider clock)
{
    public const string Open = "open";
    public const string Confirmed = "confirmed";
    public const string Discarded = "discarded";

    public async Task<Guid> CreateAsync(string name, bool fullCount, string user, CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7();
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO inventories (id, name, full_count, created_by) VALUES (@id, @name, @fullCount, @user)",
            new { id, name = string.IsNullOrWhiteSpace(name) ? "Inventário" : name.Trim(), fullCount, user });
        return id;
    }

    public async Task<IReadOnlyList<Inventory>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<Inventory>(SelectInventory + " ORDER BY i.created_at DESC")).ToList();
    }

    public async Task<Inventory?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Inventory>(SelectInventory + " WHERE i.id = @id", new { id });
    }

    public async Task<IReadOnlyList<InventoryLine>> LinesAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<InventoryLine>(
            """
            SELECT l.product_id AS ProductId, p.name AS Name, p.barcode AS Barcode, p.plu AS Plu, p.unit AS Unit,
                   l.quantity AS Quantity, l.counted_at AS CountedAt, l.counted_by AS CountedBy
            FROM inventory_lines l JOIN products p ON p.id = l.product_id
            WHERE l.inventory_id = @id
            ORDER BY l.counted_at DESC
            """, new { id })).ToList();
    }

    /// <summary>Soma à contagem do produto (cada leitura do leitor adiciona).</summary>
    public Task AddAsync(Guid id, Guid productId, decimal quantity, string user, CancellationToken ct = default) =>
        WriteLineAsync(id, productId, quantity, user, accumulate: true, ct);

    /// <summary>Corrige a quantidade contada do produto.</summary>
    public Task SetAsync(Guid id, Guid productId, decimal quantity, string user, CancellationToken ct = default) =>
        WriteLineAsync(id, productId, quantity, user, accumulate: false, ct);

    public async Task RemoveLineAsync(Guid id, Guid productId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await EnsureOpenAsync(conn, id);
        await conn.ExecuteAsync("DELETE FROM inventory_lines WHERE inventory_id = @id AND product_id = @productId", new { id, productId });
    }

    /// <summary>Contado × saldo do sistema. Em inventário geral inclui, com 0, o que tem saldo e não foi contado.</summary>
    public async Task<IReadOnlyList<InventoryReviewLine>> ReviewAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var inventory = await conn.QuerySingleAsync<(bool FullCount, string Status)>("SELECT full_count, status FROM inventories WHERE id = @id", new { id });

        var counted = await conn.QueryAsync<InventoryReviewLine>(
            """
            SELECT l.product_id AS ProductId, p.name AS Name, p.unit AS Unit,
                   coalesce(b.quantity, 0) AS Expected, l.quantity AS Counted, true AS WasCounted
            FROM inventory_lines l
            JOIN products p ON p.id = l.product_id
            LEFT JOIN stock_balances b ON b.product_id = l.product_id
            WHERE l.inventory_id = @id
            """, new { id });

        var notCounted = !inventory.FullCount ? [] : await conn.QueryAsync<InventoryReviewLine>(
            """
            SELECT p.id AS ProductId, p.name AS Name, p.unit AS Unit, b.quantity AS Expected, 0::numeric AS Counted, false AS WasCounted
            FROM stock_balances b JOIN products p ON p.id = b.product_id
            WHERE b.quantity <> 0
              AND NOT EXISTS (SELECT 1 FROM inventory_lines l WHERE l.inventory_id = @id AND l.product_id = b.product_id)
            """, new { id });

        return counted.Concat(notCounted).OrderByDescending(l => Math.Abs(l.Difference)).ThenBy(l => l.Name).ToList();
    }

    /// <summary>Confirma o inventário: grava as contagens no log de eventos. Não dá para desfazer; corrige-se com outra contagem.</summary>
    public async Task<int> ConfirmAsync(Guid id, string user, CancellationToken ct = default)
    {
        var review = await ReviewAsync(id, ct);
        var lines = (await LinesAsync(id, ct)).ToDictionary(l => l.ProductId);
        var now = clock.GetUtcNow();

        await using (var conn = await dataSource.OpenConnectionAsync(ct))
        {
            // Trava o rascunho: dois confirmes simultâneos não geram contagem em dobro.
            var locked = await conn.ExecuteAsync(
                "UPDATE inventories SET status = @Confirmed, closed_by = @user, closed_at = now() WHERE id = @id AND status = @Open",
                new { id, user, Confirmed, Open });
            if (locked == 0) throw new InvalidOperationException("Inventário não está aberto.");
        }

        try
        {
            var facts = review.Select(r => (
                r.ProductId,
                new StockCounted(r.ProductId, r.Counted, id, r.Expected, lines.TryGetValue(r.ProductId, out var l) ? l.CountedBy : user),
                (DateTimeOffset?)(lines.TryGetValue(r.ProductId, out var c) ? new DateTimeOffset(DateTime.SpecifyKind(c.CountedAt, DateTimeKind.Utc)) : now)))
                .ToList();
            if (facts.Count > 0) await events.AppendManyAsync(EventTypes.StockCounted, facts, ct);
            return facts.Count;
        }
        catch
        {
            await using var conn = await dataSource.OpenConnectionAsync(CancellationToken.None);
            await conn.ExecuteAsync("UPDATE inventories SET status = @Open, closed_by = NULL, closed_at = NULL WHERE id = @id", new { id, Open });
            throw;
        }
    }

    public async Task DiscardAsync(Guid id, string user, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE inventories SET status = @Discarded, closed_by = @user, closed_at = now() WHERE id = @id AND status = @Open",
            new { id, user, Discarded, Open });
    }

    private async Task WriteLineAsync(Guid id, Guid productId, decimal quantity, string user, bool accumulate, CancellationToken ct)
    {
        if (quantity < 0 || (accumulate && quantity == 0)) throw new ArgumentException("Quantidade inválida.");
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await EnsureOpenAsync(conn, id);
        await conn.ExecuteAsync(
            accumulate
                ? """
                  INSERT INTO inventory_lines (inventory_id, product_id, quantity, counted_at, counted_by)
                  VALUES (@id, @productId, @quantity, @at, @user)
                  ON CONFLICT (inventory_id, product_id) DO UPDATE
                    SET quantity = inventory_lines.quantity + excluded.quantity, counted_at = excluded.counted_at, counted_by = excluded.counted_by
                  """
                : """
                  INSERT INTO inventory_lines (inventory_id, product_id, quantity, counted_at, counted_by)
                  VALUES (@id, @productId, @quantity, @at, @user)
                  ON CONFLICT (inventory_id, product_id) DO UPDATE
                    SET quantity = excluded.quantity, counted_at = excluded.counted_at, counted_by = excluded.counted_by
                  """,
            new { id, productId, quantity, at = clock.GetUtcNow().UtcDateTime, user });
    }

    private static async Task EnsureOpenAsync(NpgsqlConnection conn, Guid id)
    {
        var status = await conn.ExecuteScalarAsync<string?>("SELECT status FROM inventories WHERE id = @id", new { id });
        if (status != Open) throw new InvalidOperationException("Inventário não está aberto.");
    }

    private const string SelectInventory = """
        SELECT i.id AS Id, i.name AS Name, i.full_count AS FullCount, i.status AS Status, i.created_by AS CreatedBy,
               i.created_at AS CreatedAt, i.closed_by AS ClosedBy, i.closed_at AS ClosedAt,
               (SELECT count(*)::int FROM inventory_lines l WHERE l.inventory_id = i.id) AS Lines
        FROM inventories i
        """;
}
