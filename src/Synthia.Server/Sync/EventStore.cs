using System.Text.Json;
using Dapper;
using Npgsql;
using Synthia.Contracts.Events;

namespace Synthia.Server.Sync;

/// <summary>
/// Grava lotes de eventos vindos dos caixas. Cada evento novo alimenta as projeções na mesma
/// transação; eventos repetidos são ignorados e ainda assim confirmados ao caixa.
/// </summary>
public sealed class EventStore(NpgsqlDataSource dataSource, Projector projector)
{
    public async Task<IReadOnlyList<Guid>> AppendAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var levels = new StockLevelChanges();

        foreach (var e in events.OrderBy(e => e.EventId))
        {
            var inserted = await conn.ExecuteAsync(
                """
                INSERT INTO events (event_id, stream_id, stream_seq, type, terminal_id, occurred_at, catalog_version, payload)
                VALUES (@EventId, @StreamId, @StreamSeq, @Type, @TerminalId, @OccurredAt, @CatalogVersion, @Payload::jsonb)
                ON CONFLICT (event_id) DO NOTHING
                """,
                new
                {
                    e.EventId,
                    e.StreamId,
                    e.StreamSeq,
                    e.Type,
                    e.TerminalId,
                    OccurredAt = e.OccurredAt.UtcDateTime,
                    e.CatalogVersion,
                    Payload = e.Payload.GetRawText(),
                },
                tx);

            if (inserted == 1)
                await projector.ApplyAsync(conn, tx, e, levels);
        }

        await StockLevels.ApplyAsync(conn, tx, levels);
        await tx.CommitAsync(ct);
        return events.Select(e => e.EventId).ToList();
    }

    /// <summary>Apaga as projeções e relê todo o log de eventos.</summary>
    public async Task RebuildProjectionsAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync("TRUNCATE stock_movements, stock_levels, shift_totals, nfce_queue", transaction: tx);

        var rows = await conn.QueryAsync<EventRow>(
            "SELECT event_id, stream_id, stream_seq, type, terminal_id, occurred_at, catalog_version, payload::text AS payload FROM events ORDER BY event_id",
            transaction: tx);

        // Os saldos saem de uma vez no fim, direto do ledger reconstruído.
        foreach (var row in rows)
            await projector.ApplyAsync(conn, tx, row.ToEnvelope(), new StockLevelChanges());
        await StockLevels.RebuildAsync(conn, tx);

        await tx.CommitAsync(ct);
    }

    private sealed record EventRow(
        Guid event_id, Guid stream_id, int stream_seq, string type, string terminal_id,
        DateTime occurred_at, long catalog_version, string payload)
    {
        public EventEnvelope ToEnvelope() => new(
            event_id, stream_id, stream_seq, type, terminal_id,
            new DateTimeOffset(DateTime.SpecifyKind(occurred_at, DateTimeKind.Utc)),
            catalog_version, JsonDocument.Parse(payload).RootElement.Clone());
    }
}
