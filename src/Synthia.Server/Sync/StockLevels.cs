using System.Data;
using Dapper;
using Npgsql;

namespace Synthia.Server.Sync;

/// <summary>Movimentos de estoque gravados numa transação, à espera de atualizar os saldos.</summary>
public sealed class StockLevelChanges
{
    internal List<(Guid ProductId, string Kind, decimal Quantity, DateTime At)> Movements { get; } = [];

    internal void Add(Guid productId, string kind, decimal quantity, DateTime at) => Movements.Add((productId, kind, quantity, at));
}

/// <summary>
/// Mantém <c>stock_levels</c> (saldo = última contagem + movimentos posteriores) no fim de cada transação do log.
/// Os produtos são travados em ordem de id: dois caixas sincronizando ao mesmo tempo esperam um pelo outro,
/// nunca entram em deadlock. Com a trava na mão, a contagem é recalculada lendo o ledger já com tudo
/// que os outros confirmaram; o resto entra como incremento.
/// </summary>
public static class StockLevels
{
    public static async Task ApplyAsync(NpgsqlConnection conn, IDbTransaction tx, StockLevelChanges changes)
    {
        if (changes.Movements.Count == 0) return;

        var ids = changes.Movements.Select(m => m.ProductId).Distinct().Order().ToArray();
        await conn.ExecuteAsync(
            "INSERT INTO stock_levels (product_id) SELECT id FROM unnest(@ids) AS id ORDER BY id ON CONFLICT DO NOTHING", new { ids }, tx);
        await conn.ExecuteAsync(
            "SELECT 1 FROM stock_levels WHERE product_id = ANY(@ids) ORDER BY product_id FOR UPDATE", new { ids }, tx);

        // Produto contado nesta transação: recalcula pelo ledger (que já tem os movimentos desta transação).
        var counted = changes.Movements.Where(m => m.Kind == "count").Select(m => m.ProductId).Distinct().ToArray();
        if (counted.Length > 0)
            await conn.ExecuteAsync(
                """
                WITH lc AS (
                    SELECT DISTINCT ON (product_id) product_id, quantity, occurred_at
                    FROM stock_movements WHERE kind = 'count' AND product_id = ANY(@counted)
                    ORDER BY product_id, occurred_at DESC, id DESC
                )
                UPDATE stock_levels l
                SET count_quantity = lc.quantity, count_at = lc.occurred_at,
                    delta = coalesce((SELECT sum(m.quantity) FROM stock_movements m
                                      WHERE m.product_id = l.product_id AND m.kind <> 'count' AND m.occurred_at > lc.occurred_at), 0),
                    last_movement_at = greatest(l.last_movement_at, (SELECT max(occurred_at) FROM stock_movements m WHERE m.product_id = l.product_id))
                FROM lc WHERE l.product_id = lc.product_id
                """, new { counted }, tx);

        // Demais: soma o que veio depois da contagem vigente (venda atrasada de caixa offline não conta antes dela).
        var rest = changes.Movements.Where(m => m.Kind != "count" && !counted.Contains(m.ProductId)).ToList();
        if (rest.Count > 0)
            await conn.ExecuteAsync(
                """
                UPDATE stock_levels l
                SET delta = l.delta + coalesce(v.qty, 0), last_movement_at = greatest(l.last_movement_at, v.last_at)
                FROM (
                    SELECT m.pid, sum(m.qty) FILTER (WHERE s.count_at IS NULL OR m.at > s.count_at) AS qty, max(m.at) AS last_at
                    FROM unnest(@pids, @qtys, @ats) AS m(pid, qty, at)
                    JOIN stock_levels s ON s.product_id = m.pid
                    GROUP BY m.pid
                ) v
                WHERE l.product_id = v.pid
                """,
                new
                {
                    pids = rest.Select(m => m.ProductId).ToArray(),
                    qtys = rest.Select(m => m.Quantity).ToArray(),
                    ats = rest.Select(m => m.At).ToArray(),
                }, tx);
    }

    /// <summary>Recalcula todos os saldos pelo ledger (usado no rebuild das projeções).</summary>
    public static Task RebuildAsync(NpgsqlConnection conn, IDbTransaction tx) => conn.ExecuteAsync(
        """
        INSERT INTO stock_levels (product_id, count_quantity, count_at, delta, last_movement_at)
        SELECT m.product_id, coalesce(lc.quantity, 0), lc.occurred_at,
               coalesce(sum(m.quantity) FILTER (
                   WHERE m.kind <> 'count' AND (lc.occurred_at IS NULL OR m.occurred_at > lc.occurred_at)), 0),
               max(m.occurred_at)
        FROM stock_movements m
        LEFT JOIN (
            SELECT DISTINCT ON (product_id) product_id, quantity, occurred_at
            FROM stock_movements WHERE kind = 'count'
            ORDER BY product_id, occurred_at DESC, id DESC
        ) lc ON lc.product_id = m.product_id
        GROUP BY m.product_id, lc.quantity, lc.occurred_at
        """, transaction: tx, commandTimeout: 0);
}
