using Dapper;
using Npgsql;
using Synthia.Terminal.Core.Storage;

namespace Synthia.Simulator;

/// <summary>Fotografa o estado observável (Postgres do servidor + SQLite dos caixas) para calcular o efeito de cada passo.</summary>
public sealed class Metrics(NpgsqlDataSource db, IReadOnlyList<SimTerminal> terminals)
{
    public async Task<SortedDictionary<string, string>> CaptureAsync()
    {
        var m = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var t in terminals)
        {
            m[$"{t.Id} · outbox pendente"] = t.Pusher.PendingCount().ToString();
            m[$"{t.Id} · versão do catálogo"] = t.Catalog.CurrentVersion().ToString();
            m[$"{t.Id} · turno aberto"] = t.Shifts.CurrentShiftId() is null ? "não" : "sim";
            using var conn = t.Db.Open();
            m[$"{t.Id} · eventos na outbox (total)"] = conn.ExecuteScalar<int>("SELECT count(*) FROM outbox").ToString();
        }

        await using var pg = await db.OpenConnectionAsync();
        m["Servidor · events gravados"] = (await pg.ExecuteScalarAsync<long>("SELECT count(*) FROM events")).ToString();
        m["Servidor · versão do catálogo"] = (await pg.ExecuteScalarAsync<long>("SELECT coalesce(max(version),0) FROM catalog_changes")).ToString();

        foreach (var p in await pg.QueryAsync<(string Name, long Price, bool Active)>("SELECT name, price_cents, active FROM products"))
            m[$"Servidor · cadastro · {p.Name}"] = $"{Money(p.Price)}{(p.Active ? "" : " (inativo)")}";

        foreach (var s in await pg.QueryAsync<(string Name, decimal Qty)>(
                     "SELECT coalesce(p.name, b.product_id::text), b.quantity FROM stock_balances b LEFT JOIN products p ON p.id = b.product_id"))
            m[$"Servidor · estoque · {s.Name}"] = s.Qty.ToString("0.###");

        foreach (var s in await pg.QueryAsync<ShiftRow>(
                     "SELECT terminal_id, sales_count, sales_total_cents, cash_sales_cents, withdrawals_cents, opening_cash_cents, declared_cash_cents, closed_at IS NOT NULL AS closed FROM shift_totals"))
        {
            var k = $"Servidor · turno {s.terminal_id}";
            m[$"{k} · vendas"] = $"{s.sales_count} · {Money(s.sales_total_cents)}";
            m[$"{k} · dinheiro esperado na gaveta"] = Money(s.opening_cash_cents + s.cash_sales_cents - s.withdrawals_cents);
            m[$"{k} · sangrias"] = Money(s.withdrawals_cents);
            m[$"{k} · status"] = s.closed ? $"fechado, declarado {Money(s.declared_cash_cents ?? 0)}" : "aberto";
        }

        foreach (var n in await pg.QueryAsync<(string Terminal, string Status, string? Reason)>("SELECT terminal_id, status, reason FROM nfce_queue"))
            m[$"Servidor · fila NFC-e · {n.Terminal}"] = m.TryGetValue($"Servidor · fila NFC-e · {n.Terminal}", out var prev)
                ? $"{prev}, {n.Status}" : n.Status;

        return m;
    }

    public static IReadOnlyList<Change> Diff(IDictionary<string, string> before, IDictionary<string, string> after) =>
        before.Keys.Union(after.Keys).Order(StringComparer.Ordinal)
            .Select(k => new Change(k, before.TryGetValue(k, out var b) ? b : null, after.TryGetValue(k, out var a) ? a : null))
            .Where(c => c.Before != c.After)
            .ToList();

    public static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    public static string Money(long cents) => (cents / 100m).ToString("C", PtBr);

    private sealed record ShiftRow(string terminal_id, int sales_count, long sales_total_cents, long cash_sales_cents,
        long withdrawals_cents, long opening_cash_cents, long? declared_cash_cents, bool closed);
}
