using System.Text.Json;
using Dapper;
using Npgsql;
using Synthia.Contracts;
using Synthia.Contracts.Events;

namespace Synthia.Server.Backoffice;

public sealed record StockBalance(Guid ProductId, string Name, string? Barcode, int? Plu, string Unit, decimal Quantity, DateTime? LastMovementAt, bool Active);

public sealed record StockMovement(DateTime OccurredAt, string Kind, decimal Quantity, string TerminalId, string EventType, Guid StreamId);

public sealed record BackofficeSummary(int ActiveProducts, int NegativeProducts, int Receipts, int TransferReceipts, int SalesToday, long SalesTodayCents);

public sealed record AdjustmentSummary(DateTime AdjustedAt, StockAdjusted Adjustment);

public sealed record ReceiptSummary(Guid ReceiptId, DateTime ReceivedAt, string Source, string? Supplier, string? Document, int ItemCount, decimal TotalQuantity, string ReceivedBy);

/// <summary>Entradas de mercadoria e consulta de estoque da retaguarda.</summary>
public sealed class StockService(NpgsqlDataSource dataSource, BackofficeEvents events)
{
    public async Task<StockReceived> ReceiveAsync(
        string source, string? supplier, string? document, IReadOnlyList<StockReceiptItem> items, string receivedBy, string? note,
        CancellationToken ct = default) =>
        await ReceiveAsync(source, supplier, document, items, receivedBy, note, accessKey: null, ct);

    public async Task<StockReceived> ReceiveAsync(
        string source, string? supplier, string? document, IReadOnlyList<StockReceiptItem> items, string receivedBy, string? note,
        string? accessKey, CancellationToken ct = default)
    {
        if (items.Count == 0) throw new ArgumentException("Entrada sem itens.");
        if (items.Any(i => i.Quantity <= 0)) throw new ArgumentException("Quantidades devem ser positivas.");

        var receipt = new StockReceived(Guid.CreateVersion7(), source, Blank(supplier), Blank(document), items, receivedBy, Blank(note), accessKey);
        await events.AppendAsync(EventTypes.StockReceived, receipt.ReceiptId, receipt, ct);
        return receipt;
    }

    public async Task<StockAdjusted> AdjustAsync(string reason, IReadOnlyList<StockAdjustmentItem> items, string adjustedBy, string? note, CancellationToken ct = default)
    {
        if (!AdjustmentReasons.All.Contains(reason)) throw new ArgumentException("Motivo inválido.");
        if (items.Count == 0) throw new ArgumentException("Ajuste sem itens.");
        if (items.Any(i => i.Quantity <= 0)) throw new ArgumentException("Quantidades devem ser positivas.");

        var adjustment = new StockAdjusted(Guid.CreateVersion7(), reason, items, adjustedBy, Blank(note));
        await events.AppendAsync(EventTypes.StockAdjusted, adjustment.AdjustmentId, adjustment, ct);
        return adjustment;
    }

    public async Task<IReadOnlyList<AdjustmentSummary>> ListAdjustmentsAsync(int limit = 100, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<(DateTime OccurredAt, string Payload)>(
            "SELECT occurred_at, payload::text FROM events WHERE type = @type ORDER BY event_id DESC LIMIT @limit",
            new { type = EventTypes.StockAdjusted, limit });
        return rows.Select(r => new AdjustmentSummary(r.OccurredAt, JsonSerializer.Deserialize<StockAdjusted>(r.Payload, SynthiaJson.Options)!)).ToList();
    }

    public async Task<IReadOnlyList<ReceiptSummary>> ListReceiptsAsync(int limit = 100, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<(DateTime OccurredAt, string Payload)>(
            "SELECT occurred_at, payload::text FROM events WHERE type = @type ORDER BY event_id DESC LIMIT @limit",
            new { type = EventTypes.StockReceived, limit });

        return rows.Select(r =>
        {
            var p = JsonSerializer.Deserialize<StockReceived>(r.Payload, SynthiaJson.Options)!;
            return new ReceiptSummary(p.ReceiptId, r.OccurredAt, p.Source, p.Supplier, p.Document, p.Items.Count, p.Items.Sum(i => i.Quantity), p.ReceivedBy);
        }).ToList();
    }

    public async Task<StockReceived?> GetReceiptAsync(Guid receiptId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var json = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT payload::text FROM events WHERE type = @type AND stream_id = @receiptId",
            new { type = EventTypes.StockReceived, receiptId });
        return json is null ? null : JsonSerializer.Deserialize<StockReceived>(json, SynthiaJson.Options);
    }

    /// <summary>Saldo de todos os produtos do cadastro (sem movimento = 0).</summary>
    public async Task<IReadOnlyList<StockBalance>> BalancesAsync(string? search = null, bool onlyNegative = false, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<StockBalance>(
            """
            SELECT p.id AS ProductId, p.name AS Name, p.barcode AS Barcode, p.plu AS Plu, p.unit AS Unit,
                   coalesce(b.quantity, 0) AS Quantity,
                   b.last_movement_at AS LastMovementAt,
                   p.active AS Active
            FROM products p
            LEFT JOIN stock_balances b ON b.product_id = p.id
            WHERE (@search IS NULL OR p.name ILIKE '%' || @search || '%' OR p.barcode = @search OR p.plu::text = @search)
              AND (NOT @onlyNegative OR coalesce(b.quantity, 0) < 0)
            ORDER BY p.name
            """,
            new { search = Blank(search), onlyNegative })).ToList();
    }

    /// <summary>Histórico de movimentos de um produto (o "kardex"), mais recente primeiro.</summary>
    public async Task<IReadOnlyList<StockMovement>> HistoryAsync(Guid productId, int limit = 200, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<StockMovement>(
            """
            SELECT m.occurred_at AS OccurredAt, m.kind AS Kind, m.quantity AS Quantity,
                   e.terminal_id AS TerminalId, e.type AS EventType, e.stream_id AS StreamId
            FROM stock_movements m JOIN events e ON e.event_id = m.event_id
            WHERE m.product_id = @productId
            ORDER BY m.occurred_at DESC, m.id DESC
            LIMIT @limit
            """,
            new { productId, limit })).ToList();
    }

    public async Task<BackofficeSummary> SummaryAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleAsync<BackofficeSummary>(
            """
            SELECT (SELECT count(*)::int FROM products WHERE active) AS ActiveProducts,
                   (SELECT count(*)::int FROM stock_balances WHERE quantity < 0) AS NegativeProducts,
                   (SELECT count(*)::int FROM events WHERE type = @received) AS Receipts,
                   (SELECT count(*)::int FROM events WHERE type = @received AND payload->>'source' = 'transfer') AS TransferReceipts,
                   (SELECT count(*)::int FROM events WHERE type = @sale AND occurred_at >= date_trunc('day', now())) AS SalesToday,
                   (SELECT coalesce(sum((payload->>'totalCents')::bigint), 0)::bigint FROM events
                     WHERE type = @sale AND occurred_at >= date_trunc('day', now())) AS SalesTodayCents
            """,
            new { received = EventTypes.StockReceived, sale = EventTypes.SaleCompleted });
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
