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

    /// <summary>
    /// Agrega no Postgres sobre a projeção de vendas (sales, sale_items, sale_payments) e traz só os totais:
    /// tempo e memória não dependem de reler o JSON do log. Horas em UTC voltam para o C# e viram dia e hora
    /// no fuso da loja.
    /// </summary>
    /// <param name="topProducts">Quantos produtos listar (exportação usa todos).</param>
    public async Task<SalesReport> SalesAsync(DateOnly from, DateOnly to, string? terminalId = null, CancellationToken ct = default, int topProducts = 20)
    {
        var (start, end) = Range(from, to);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var args = new { start, end, terminalId, topProducts, cash = PaymentMethods.Cash };
        const string period = "s.occurred_at >= @start AND s.occurred_at < @end AND (@terminalId::text IS NULL OR s.terminal_id = @terminalId)";

        // Vendas por hora e caixa. Cancelada sai inteira; devolução sai pelo valor dos itens devolvidos.
        var buckets = (await conn.QueryAsync<(DateTime Hour, string TerminalId, int Sales, long Gross, long Cancelled)>(
            $"""
            SELECT date_trunc('hour', s.occurred_at) AS hour, s.terminal_id,
                   count(*) FILTER (WHERE NOT s.cancelled)::int AS sales,
                   sum(s.total_cents)::bigint AS gross,
                   sum(CASE WHEN s.cancelled THEN s.total_cents ELSE coalesce(r.cents, 0) END)::bigint AS cancelled
            FROM sales s
            LEFT JOIN LATERAL (SELECT sum(i.total_cents) AS cents FROM sale_items i WHERE i.sale_id = s.sale_id AND i.refunded) r ON true
            WHERE {period}
            GROUP BY 1, 2
            """, args, commandTimeout: 0)).ToList();

        var products = (await conn.QueryAsync<ProductTotal>(
            $"""
            SELECT i.product_id AS ProductId, (array_agg(i.description))[1] AS Description,
                   trim_scale(sum(i.quantity)) AS Quantity, sum(i.total_cents)::bigint AS NetCents
            FROM sales s JOIN sale_items i ON i.sale_id = s.sale_id
            WHERE {period} AND NOT s.cancelled AND NOT i.refunded
            GROUP BY i.product_id
            ORDER BY sum(i.total_cents) DESC, 2
            LIMIT @topProducts
            """, args, commandTimeout: 0)).ToList();

        // Forma de pagamento: o que ficou com a loja (dinheiro sem o troco; devoluções saem pela forma usada).
        var payments = (await conn.QueryAsync<PaymentTotal>(
            $"""
            SELECT method AS Method, sum(n)::int AS Count, sum(cents)::bigint AS NetCents FROM (
                SELECT p.method, 1 AS n, p.amount_cents - CASE WHEN p.method = @cash THEN s.change_cents ELSE 0 END AS cents
                FROM sales s JOIN sale_payments p ON p.sale_id = s.sale_id
                WHERE {period} AND NOT s.cancelled
                UNION ALL
                SELECT i.refund_method, 0, -i.total_cents
                FROM sales s JOIN sale_items i ON i.sale_id = s.sale_id AND i.refunded
                WHERE {period} AND NOT s.cancelled
            ) x
            WHERE method IS NOT NULL
            GROUP BY method
            ORDER BY 3 DESC
            """, args, commandTimeout: 0)).ToList();

        var cancelledSales = await conn.ExecuteScalarAsync<int>($"SELECT count(*) FROM sales s WHERE {period} AND s.cancelled", args);

        var days = new SortedDictionary<DateOnly, (int Sales, long Cents)>();
        var terminals = new SortedDictionary<string, (int Sales, long Cents)>();
        var byHour = new long[24];
        foreach (var b in buckets.Where(b => b.Sales > 0))
        {
            var local = Local(b.Hour);
            var net = b.Gross - b.Cancelled;
            Add(days, DateOnly.FromDateTime(local), b.Sales, net);
            Add(terminals, b.TerminalId, b.Sales, net);
            byHour[local.Hour] += net;
        }

        var gross = buckets.Sum(b => b.Gross);
        var cancelled = buckets.Sum(b => b.Cancelled);
        return new SalesReport(
            from, to, terminalId, buckets.Sum(b => b.Sales), cancelledSales, gross, cancelled, gross - cancelled,
            payments,
            days.Select(d => new DayTotal(d.Key, d.Value.Sales, d.Value.Cents)).ToList(),
            terminals.Select(t => new TerminalTotal(t.Key, t.Value.Sales, t.Value.Cents)).ToList(),
            byHour,
            products);
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

    private static void Add<TKey>(SortedDictionary<TKey, (int Sales, long Cents)> map, TKey key, int sales, long cents) where TKey : notnull
    {
        var v = map.GetValueOrDefault(key);
        map[key] = (v.Sales + sales, v.Cents + cents);
    }
}
