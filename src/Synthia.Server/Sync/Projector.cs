using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using Synthia.Contracts;
using Synthia.Contracts.Events;

namespace Synthia.Server.Sync;

/// <summary>Deriva estoque, totais de turno e fila de NFC-e a partir de cada evento.</summary>
public sealed class Projector(ILogger<Projector> logger)
{
    /// <param name="levels">Recebe os movimentos de estoque gravados; o chamador atualiza os saldos antes do commit.</param>
    public async Task ApplyAsync(NpgsqlConnection conn, IDbTransaction tx, EventEnvelope e, StockLevelChanges levels)
    {
        var at = e.OccurredAt.UtcDateTime;

        switch (e.Type)
        {
            case EventTypes.ShiftOpened:
            {
                var p = SynthiaJson.FromElement<ShiftOpened>(e.Payload);
                await conn.ExecuteAsync(
                    """
                    INSERT INTO shift_totals (shift_id, terminal_id, operator, opened_at, opening_cash_cents)
                    VALUES (@ShiftId, @TerminalId, @Operator, @At, @OpeningCashCents)
                    ON CONFLICT (shift_id) DO UPDATE
                      SET operator = excluded.operator, opened_at = excluded.opened_at,
                          opening_cash_cents = excluded.opening_cash_cents
                    """,
                    new { p.ShiftId, e.TerminalId, p.Operator, At = at, p.OpeningCashCents }, tx);
                break;
            }

            case EventTypes.SaleCompleted:
            {
                var p = SynthiaJson.FromElement<SaleCompleted>(e.Payload);
                foreach (var item in p.Items)
                    await InsertMovementAsync(conn, tx, e, levels, item.ProductId, item.ItemId, "sale", -item.Quantity);

                var cash = p.Payments.Where(x => x.Method == PaymentMethods.Cash).Sum(x => x.AmountCents) - p.ChangeCents;
                await AddToShiftAsync(conn, tx, p.ShiftId, e.TerminalId, salesCount: 1, p.TotalCents, cash, withdrawals: 0);

                if (p.NfceStatus is NfceStatus.Pending or NfceStatus.Rejected)
                    await UpsertNfceAsync(conn, tx, p.SaleId, e.TerminalId, p.NfceKey, p.NfceStatus, reason: null, at);
                break;
            }

            case EventTypes.SaleItemCancelled:
            {
                var p = SynthiaJson.FromElement<SaleItemCancelled>(e.Payload);
                var sale = await FindSaleAsync(conn, tx, p.SaleId);
                var item = sale?.Items.FirstOrDefault(i => i.ItemId == p.ItemId);
                if (sale is null || item is null)
                {
                    logger.LogWarning("Cancelamento do item {ItemId} sem venda {SaleId} correspondente", p.ItemId, p.SaleId);
                    break;
                }

                if (await IsItemReturnedAsync(conn, tx, item.ItemId)) break;
                await InsertMovementAsync(conn, tx, e, levels, item.ProductId, item.ItemId, "item_cancelled", item.Quantity);
                var cashRefund = p.RefundMethod == PaymentMethods.Cash ? item.TotalCents : 0;
                await AddToShiftAsync(conn, tx, sale.ShiftId, e.TerminalId, 0, -item.TotalCents, -cashRefund, 0);
                break;
            }

            case EventTypes.SaleCancelled:
            {
                var p = SynthiaJson.FromElement<SaleCancelled>(e.Payload);
                var sale = await FindSaleAsync(conn, tx, p.SaleId);
                if (sale is null)
                {
                    logger.LogWarning("Cancelamento da venda {SaleId} sem SaleCompleted correspondente", p.SaleId);
                    break;
                }

                long returnedCents = 0;
                foreach (var item in sale.Items)
                {
                    if (await IsItemReturnedAsync(conn, tx, item.ItemId)) continue;
                    await InsertMovementAsync(conn, tx, e, levels, item.ProductId, item.ItemId, "sale_cancelled", item.Quantity);
                    returnedCents += item.TotalCents;
                }

                // Devolve o dinheiro que ainda está na gaveta: o líquido da venda menos o que já
                // saiu em devoluções de item.
                var cash = sale.Payments.Where(x => x.Method == PaymentMethods.Cash).Sum(x => x.AmountCents) - sale.ChangeCents
                           - await CashAlreadyRefundedAsync(conn, tx, sale, before: e.EventId);
                await AddToShiftAsync(conn, tx, sale.ShiftId, e.TerminalId, -1, -returnedCents, -cash, 0);
                await conn.ExecuteAsync("DELETE FROM nfce_queue WHERE sale_id = @SaleId", new { p.SaleId }, tx);
                break;
            }

            case EventTypes.CashWithdrawal:
            {
                var p = SynthiaJson.FromElement<CashWithdrawal>(e.Payload);
                await AddToShiftAsync(conn, tx, p.ShiftId, e.TerminalId, 0, 0, 0, p.AmountCents);
                break;
            }

            case EventTypes.ShiftClosed:
            {
                var p = SynthiaJson.FromElement<ShiftClosed>(e.Payload);
                await conn.ExecuteAsync(
                    """
                    INSERT INTO shift_totals (shift_id, terminal_id, declared_cash_cents, closed_at)
                    VALUES (@ShiftId, @TerminalId, @DeclaredCashCents, @At)
                    ON CONFLICT (shift_id) DO UPDATE
                      SET declared_cash_cents = excluded.declared_cash_cents, closed_at = excluded.closed_at
                    """,
                    new { p.ShiftId, e.TerminalId, p.DeclaredCashCents, At = at }, tx);
                break;
            }

            case EventTypes.StockCounted:
            {
                var p = SynthiaJson.FromElement<StockCounted>(e.Payload);
                await InsertMovementAsync(conn, tx, e, levels, p.ProductId, null, "count", p.CountedQuantity);
                break;
            }

            case EventTypes.StockReceived:
            {
                var p = SynthiaJson.FromElement<StockReceived>(e.Payload);
                foreach (var item in p.Items)
                    await InsertMovementAsync(conn, tx, e, levels, item.ProductId, null, "received", item.Quantity);
                break;
            }

            case EventTypes.StockAdjusted:
            {
                var p = SynthiaJson.FromElement<StockAdjusted>(e.Payload);
                foreach (var item in p.Items)
                    await InsertMovementAsync(conn, tx, e, levels, item.ProductId, null, "adjusted", -item.Quantity);
                break;
            }

            case EventTypes.NfceAuthorized:
            {
                var p = SynthiaJson.FromElement<NfceAuthorized>(e.Payload);
                await conn.ExecuteAsync("DELETE FROM nfce_queue WHERE sale_id = @SaleId", new { p.SaleId }, tx);
                break;
            }

            case EventTypes.NfceRejected:
            {
                var p = SynthiaJson.FromElement<NfceRejected>(e.Payload);
                await UpsertNfceAsync(conn, tx, p.SaleId, e.TerminalId, p.NfceKey, NfceStatus.Rejected, $"{p.Code}: {p.Reason}", at);
                break;
            }

            default:
                // Tipos novos ficam no log e podem ser projetados depois com RebuildProjectionsAsync.
                logger.LogInformation("Evento {Type} sem projeção", e.Type);
                break;
        }
    }

    private static Task InsertMovementAsync(
        NpgsqlConnection conn, IDbTransaction tx, EventEnvelope e, StockLevelChanges levels, Guid productId, Guid? itemId, string kind, decimal quantity)
    {
        levels.Add(productId, kind, quantity, e.OccurredAt.UtcDateTime);
        return conn.ExecuteAsync(
            """
            INSERT INTO stock_movements (event_id, product_id, item_id, kind, quantity, occurred_at)
            VALUES (@EventId, @ProductId, @ItemId, @Kind, @Quantity, @At)
            """,
            new { e.EventId, ProductId = productId, ItemId = itemId, Kind = kind, Quantity = quantity, At = e.OccurredAt.UtcDateTime },
            tx);
    }

    private static async Task<bool> IsItemReturnedAsync(NpgsqlConnection conn, IDbTransaction tx, Guid itemId) =>
        await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM stock_movements WHERE item_id = @itemId AND kind IN ('item_cancelled', 'sale_cancelled'))",
            new { itemId }, tx);

    private static async Task<long> CashAlreadyRefundedAsync(NpgsqlConnection conn, IDbTransaction tx, SaleCompleted sale, Guid before)
    {
        var cancelled = await conn.QueryAsync<string>(
            "SELECT payload::text FROM events WHERE type = @Type AND stream_id = @SaleId AND event_id < @before",
            new { Type = EventTypes.SaleItemCancelled, sale.SaleId, before }, tx);

        var cashItems = cancelled
            .Select(json => JsonSerializer.Deserialize<SaleItemCancelled>(json, SynthiaJson.Options)!)
            .Where(c => c.RefundMethod == PaymentMethods.Cash)
            .Select(c => c.ItemId)
            .ToHashSet();

        return sale.Items.Where(i => cashItems.Contains(i.ItemId)).Sum(i => i.TotalCents);
    }

    private static async Task<SaleCompleted?> FindSaleAsync(NpgsqlConnection conn, IDbTransaction tx, Guid saleId)
    {
        var json = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT payload::text FROM events WHERE type = @Type AND stream_id = @saleId",
            new { Type = EventTypes.SaleCompleted, saleId }, tx);

        return json is null ? null : JsonSerializer.Deserialize<SaleCompleted>(json, SynthiaJson.Options);
    }

    private static Task AddToShiftAsync(
        NpgsqlConnection conn, IDbTransaction tx, Guid shiftId, string terminalId,
        int salesCount, long salesTotal, long cashSales, long withdrawals) =>
        conn.ExecuteAsync(
            """
            INSERT INTO shift_totals (shift_id, terminal_id, sales_count, sales_total_cents, cash_sales_cents, withdrawals_cents)
            VALUES (@shiftId, @terminalId, @salesCount, @salesTotal, @cashSales, @withdrawals)
            ON CONFLICT (shift_id) DO UPDATE SET
              sales_count       = shift_totals.sales_count + excluded.sales_count,
              sales_total_cents = shift_totals.sales_total_cents + excluded.sales_total_cents,
              cash_sales_cents  = shift_totals.cash_sales_cents + excluded.cash_sales_cents,
              withdrawals_cents = shift_totals.withdrawals_cents + excluded.withdrawals_cents
            """,
            new { shiftId, terminalId, salesCount, salesTotal, cashSales, withdrawals }, tx);

    private static Task UpsertNfceAsync(
        NpgsqlConnection conn, IDbTransaction tx, Guid saleId, string terminalId,
        string? nfceKey, string status, string? reason, DateTime at) =>
        conn.ExecuteAsync(
            """
            INSERT INTO nfce_queue (sale_id, terminal_id, nfce_key, status, reason, updated_at)
            VALUES (@saleId, @terminalId, @nfceKey, @status, @reason, @at)
            ON CONFLICT (sale_id) DO UPDATE
              SET nfce_key = excluded.nfce_key, status = excluded.status,
                  reason = excluded.reason, updated_at = excluded.updated_at
            """,
            new { saleId, terminalId, nfceKey, status, reason, at }, tx);
}
