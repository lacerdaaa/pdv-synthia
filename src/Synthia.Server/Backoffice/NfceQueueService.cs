using System.Text.Json;
using Dapper;
using Npgsql;
using Synthia.Contracts;
using Synthia.Contracts.Events;

namespace Synthia.Server.Backoffice;

/// <param name="Ncm">NCM atual do cadastro (pode já ter sido corrigido depois da venda).</param>
public sealed record NfceQueueLine(Guid ProductId, string Description, decimal Quantity, long TotalCents, string? Ncm);

public sealed record NfceQueueEntry(
    Guid SaleId, string TerminalId, string? Key, string Status, string? Reason, DateTime UpdatedAt,
    DateTime? SoldAt, long TotalCents, IReadOnlyList<NfceQueueLine> Lines)
{
    /// <summary>Prazo para transmitir a contingência offline (em geral 24 h; confirmar com a SEFAZ-SP).</summary>
    public static readonly TimeSpan ContingencyDeadline = TimeSpan.FromHours(24);

    public DateTime? DeadlineAt => Status == NfceStatus.Pending && SoldAt is { } s ? s + ContingencyDeadline : null;

    public bool Overdue(DateTime nowUtc) => DeadlineAt is { } d && nowUtc > d;

    /// <summary>Itens cujo NCM do cadastro está ausente ou fora de 8 dígitos: suspeitos de causar rejeição.</summary>
    public IEnumerable<NfceQueueLine> SuspectLines => Lines.Where(l => l.Ncm is null || l.Ncm == "00000000" || l.Ncm.Length != 8);
}

public sealed record NfceQueueSummary(int Pending, int Rejected, int Overdue);

/// <summary>
/// NFC-e que pedem atenção: emitidas em contingência ainda não transmitidas e rejeitadas pela SEFAZ.
/// A transmissão e o reenvio são feitos pelo caixa (ACBr); a retaguarda acompanha e corrige o cadastro.
/// </summary>
public sealed class NfceQueueService(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<IReadOnlyList<NfceQueueEntry>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<(Guid SaleId, string TerminalId, string? Key, string Status, string? Reason, DateTime UpdatedAt, DateTime? SoldAt, string? Payload)>(
            """
            SELECT q.sale_id, q.terminal_id, q.nfce_key, q.status, q.reason, q.updated_at, e.occurred_at, e.payload::text
            FROM nfce_queue q
            LEFT JOIN events e ON e.stream_id = q.sale_id AND e.type = @sale
            ORDER BY CASE q.status WHEN 'rejected' THEN 0 ELSE 1 END, e.occurred_at
            """, new { sale = EventTypes.SaleCompleted });

        var ncms = (await conn.QueryAsync<(Guid Id, string Ncm)>("SELECT id, ncm FROM products")).ToDictionary(p => p.Id, p => p.Ncm);

        return rows.Select(r =>
        {
            var sale = r.Payload is null ? null : JsonSerializer.Deserialize<SaleCompleted>(r.Payload, SynthiaJson.Options);
            var lines = sale?.Items.Select(i => new NfceQueueLine(i.ProductId, i.Description, i.Quantity, i.TotalCents, ncms.GetValueOrDefault(i.ProductId))).ToList() ?? [];
            return new NfceQueueEntry(r.SaleId, r.TerminalId, r.Key, r.Status, r.Reason, r.UpdatedAt, r.SoldAt, sale?.TotalCents ?? 0, lines);
        }).ToList();
    }

    public async Task<NfceQueueSummary> SummaryAsync(CancellationToken ct = default)
    {
        var list = await ListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        return new NfceQueueSummary(
            list.Count(e => e.Status == NfceStatus.Pending),
            list.Count(e => e.Status == NfceStatus.Rejected),
            list.Count(e => e.Overdue(now)));
    }
}
