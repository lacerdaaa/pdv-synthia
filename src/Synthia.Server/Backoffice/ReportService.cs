using System.Text.Json;
using Dapper;
using Npgsql;
using Synthia.Contracts;
using Synthia.Contracts.Events;

namespace Synthia.Server.Backoffice;

public sealed record PaymentTotal(string Method, int Count, long NetCents);
public sealed record DayTotal(DateOnly Day, int Sales, long NetCents);
public sealed record TerminalTotal(string TerminalId, int Sales, long NetCents);
public sealed record ProductTotal(Guid ProductId, string Description, decimal Quantity, long NetCents);

public sealed record SalesReport(
    DateOnly From, DateOnly To, string? TerminalId,
    int Sales, int CancelledSales, long GrossCents, long CancelledCents, long NetCents,
    IReadOnlyList<PaymentTotal> Payments, IReadOnlyList<DayTotal> Days, IReadOnlyList<TerminalTotal> Terminals,
    IReadOnlyList<long> NetByHour, IReadOnlyList<ProductTotal> TopProducts)
{
    public long AverageTicketCents => Sales == 0 ? 0 : NetCents / Sales;
}

/// <param name="ExpectedCashCents">Fundo + vendas em dinheiro (líquidas de troco e devoluções) − sangrias.</param>
public sealed record ShiftReview(
    Guid ShiftId, string TerminalId, string? Operator, DateTime? OpenedAt, DateTime? ClosedAt,
    int Sales, long SalesCents, long OpeningCashCents, long CashSalesCents, long WithdrawalsCents, long? DeclaredCashCents)
{
    public long ExpectedCashCents => OpeningCashCents + CashSalesCents - WithdrawalsCents;
    public long? DifferenceCents => DeclaredCashCents is { } d ? d - ExpectedCashCents : null;
    public bool Closed => ClosedAt is not null;
}

/// <summary>
/// Relatórios da retaguarda, calculados a partir do log de eventos (vendas e cancelamentos) e das
/// projeções de turno. Datas no fuso da loja (Store:TimeZone, padrão America/Sao_Paulo).
/// </summary>
public sealed class ReportService(NpgsqlDataSource dataSource, IConfiguration config)
{
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(config["Store:TimeZone"] ?? "America/Sao_Paulo");

    public DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

    public async Task<SalesReport> SalesAsync(DateOnly from, DateOnly to, string? terminalId = null, CancellationToken ct = default)
    {
        var (start, end) = Range(from, to);
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var sales = (await conn.QueryAsync<(string TerminalId, DateTime At, string Payload)>(
                """
                SELECT terminal_id, occurred_at, payload::text FROM events
                WHERE type = @type AND occurred_at >= @start AND occurred_at < @end
                  AND (@terminalId IS NULL OR terminal_id = @terminalId)
                """, new { type = EventTypes.SaleCompleted, start, end, terminalId }))
            .Select(r => (r.TerminalId, At: Local(r.At), Sale: JsonSerializer.Deserialize<SaleCompleted>(r.Payload, SynthiaJson.Options)!))
            .ToList();

        // Cancelamentos das vendas do período, quando quer que tenham acontecido.
        var ids = sales.Select(s => s.Sale.SaleId).ToArray();
        var cancels = (await conn.QueryAsync<(Guid SaleId, string Type, string Payload)>(
            "SELECT stream_id, type, payload::text FROM events WHERE stream_id = ANY(@ids) AND type IN (@item, @sale) ORDER BY event_id",
            new { ids, item = EventTypes.SaleItemCancelled, sale = EventTypes.SaleCancelled })).ToList();
        var cancelledSales = cancels.Where(c => c.Type == EventTypes.SaleCancelled).Select(c => c.SaleId).ToHashSet();
        var cancelledItems = cancels.Where(c => c.Type == EventTypes.SaleItemCancelled)
            .Select(c => JsonSerializer.Deserialize<SaleItemCancelled>(c.Payload, SynthiaJson.Options)!)
            .GroupBy(c => c.SaleId).ToDictionary(g => g.Key, g => g.ToList());

        var payments = new Dictionary<string, (int Count, long Cents)>();
        var days = new SortedDictionary<DateOnly, (int Sales, long Cents)>();
        var terminals = new SortedDictionary<string, (int Sales, long Cents)>();
        var products = new Dictionary<Guid, (string Description, decimal Qty, long Cents)>();
        var byHour = new long[24];
        long gross = 0, cancelled = 0;
        var count = 0;

        foreach (var (terminal, at, sale) in sales)
        {
            gross += sale.TotalCents;
            if (cancelledSales.Contains(sale.SaleId)) { cancelled += sale.TotalCents; continue; }

            var refunds = cancelledItems.GetValueOrDefault(sale.SaleId) ?? [];
            var refundedIds = refunds.Select(r => r.ItemId).ToHashSet();
            var refundedCents = sale.Items.Where(i => refundedIds.Contains(i.ItemId)).Sum(i => i.TotalCents);
            var net = sale.TotalCents - refundedCents;
            cancelled += refundedCents;
            count++;

            Add(days, DateOnly.FromDateTime(at), net);
            Add(terminals, terminal, net);
            byHour[at.Hour] += net;

            foreach (var item in sale.Items.Where(i => !refundedIds.Contains(i.ItemId)))
            {
                var p = products.GetValueOrDefault(item.ProductId, (item.Description, 0m, 0L));
                products[item.ProductId] = (item.Description, p.Item2 + item.Quantity, p.Item3 + item.TotalCents);
            }

            // Forma de pagamento: o que ficou com a loja (dinheiro sem o troco; devoluções saem pela forma usada).
            foreach (var pay in sale.Payments)
            {
                var amount = pay.AmountCents - (pay.Method == PaymentMethods.Cash ? sale.ChangeCents : 0);
                var p = payments.GetValueOrDefault(pay.Method);
                payments[pay.Method] = (p.Count + 1, p.Cents + amount);
            }
            foreach (var refund in refunds)
            {
                var item = sale.Items.FirstOrDefault(i => i.ItemId == refund.ItemId);
                var method = refund.RefundMethod ?? sale.Payments.FirstOrDefault()?.Method;
                if (item is null || method is null) continue;
                var p = payments.GetValueOrDefault(method);
                payments[method] = (p.Count, p.Cents - item.TotalCents);
            }
        }

        return new SalesReport(
            from, to, terminalId, count, cancelledSales.Count, gross, cancelled, gross - cancelled,
            payments.Select(p => new PaymentTotal(p.Key, p.Value.Count, p.Value.Cents)).OrderByDescending(p => p.NetCents).ToList(),
            days.Select(d => new DayTotal(d.Key, d.Value.Sales, d.Value.Cents)).ToList(),
            terminals.Select(t => new TerminalTotal(t.Key, t.Value.Sales, t.Value.Cents)).ToList(),
            byHour,
            products.Select(p => new ProductTotal(p.Key, p.Value.Description, p.Value.Qty, p.Value.Cents))
                .OrderByDescending(p => p.NetCents).Take(20).ToList());
    }

    public async Task<IReadOnlyList<ShiftReview>> ShiftsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (start, end) = Range(from, to);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<ShiftReview>(
            """
            SELECT shift_id AS ShiftId, terminal_id AS TerminalId, operator AS Operator, opened_at AS OpenedAt, closed_at AS ClosedAt,
                   sales_count AS Sales, sales_total_cents AS SalesCents, opening_cash_cents AS OpeningCashCents,
                   cash_sales_cents AS CashSalesCents, withdrawals_cents AS WithdrawalsCents, declared_cash_cents AS DeclaredCashCents
            FROM shift_totals
            WHERE (opened_at >= @start AND opened_at < @end) OR closed_at IS NULL
            ORDER BY opened_at DESC NULLS LAST
            """, new { start, end })).ToList();
    }

    private (DateTime Start, DateTime End) Range(DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), Zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), Zone);
        return (start, end);
    }

    private DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    private static void Add<TKey>(SortedDictionary<TKey, (int Sales, long Cents)> map, TKey key, long cents) where TKey : notnull
    {
        var v = map.GetValueOrDefault(key);
        map[key] = (v.Sales + 1, v.Cents + cents);
    }
}
