using System.Data;
using System.Text.Json;
using Dapper;
using Synthia.Contracts;
using Synthia.Contracts.Events;

namespace Synthia.Terminal.Core.Storage;

/// <summary>
/// Fila de eventos a enviar. Sempre gravada na mesma transação do fato que a gerou:
/// se a transação falha, nem o fato nem o evento existem.
/// </summary>
public sealed class Outbox(TerminalOptions options, TimeProvider clock)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public EventEnvelope Append<T>(IDbConnection conn, IDbTransaction tx, string type, Guid streamId, T payload)
    {
        var seq = conn.ExecuteScalar<int>(
            "SELECT coalesce(max(stream_seq), 0) + 1 FROM outbox WHERE stream_id = @streamId", new { streamId }, tx);

        var envelope = new EventEnvelope(
            Guid.CreateVersion7(clock.GetUtcNow()),
            streamId,
            seq,
            type,
            options.TerminalId,
            clock.GetUtcNow(),
            Meta.GetCatalogVersion(conn, tx),
            SynthiaJson.ToElement(payload));

        conn.Execute(
            """
            INSERT INTO outbox (event_id, stream_id, stream_seq, type, terminal_id, occurred_at, catalog_version, payload)
            VALUES (@EventId, @StreamId, @StreamSeq, @Type, @TerminalId, @OccurredAt, @CatalogVersion, @Payload)
            """,
            new
            {
                envelope.EventId,
                envelope.StreamId,
                envelope.StreamSeq,
                envelope.Type,
                envelope.TerminalId,
                OccurredAt = LocalDb.ToDbTime(envelope.OccurredAt),
                envelope.CatalogVersion,
                Payload = envelope.Payload.GetRawText(),
            },
            tx);

        return envelope;
    }

    public static int PendingCount(IDbConnection conn) =>
        conn.ExecuteScalar<int>("SELECT count(*) FROM outbox WHERE sent_at IS NULL");

    public static IReadOnlyList<EventEnvelope> TakePending(IDbConnection conn, int limit) =>
        conn.Query<Row>(
                """
                SELECT event_id AS EventId, stream_id AS StreamId, stream_seq AS StreamSeq, type AS Type,
                       terminal_id AS TerminalId, occurred_at AS OccurredAt, catalog_version AS CatalogVersion, payload AS Payload
                FROM outbox WHERE sent_at IS NULL ORDER BY event_id LIMIT @limit
                """,
                new { limit })
            .Select(r => r.ToEnvelope())
            .ToList();

    public void MarkSent(IDbConnection conn, IEnumerable<Guid> eventIds)
    {
        using var tx = conn.BeginTransaction();
        var sentAt = LocalDb.ToDbTime(clock.GetUtcNow());
        foreach (var id in eventIds)
            conn.Execute("UPDATE outbox SET sent_at = @sentAt WHERE event_id = @id AND sent_at IS NULL", new { sentAt, id }, tx);
        tx.Commit();
    }

    /// <summary>Eventos já confirmados ficam 30 dias para permitir replay se o servidor perder o disco.</summary>
    public int Prune(IDbConnection conn)
    {
        var cutoff = LocalDb.ToDbTime(clock.GetUtcNow() - Retention);
        return conn.Execute("DELETE FROM outbox WHERE sent_at IS NOT NULL AND sent_at < @cutoff", new { cutoff });
    }

    private sealed class Row
    {
        public string EventId { get; init; } = "";
        public string StreamId { get; init; } = "";
        public int StreamSeq { get; init; }
        public string Type { get; init; } = "";
        public string TerminalId { get; init; } = "";
        public string OccurredAt { get; init; } = "";
        public long CatalogVersion { get; init; }
        public string Payload { get; init; } = "";

        public EventEnvelope ToEnvelope() => new(
            Guid.Parse(EventId), Guid.Parse(StreamId), StreamSeq, Type, TerminalId, LocalDb.FromDbTime(OccurredAt), CatalogVersion,
            JsonDocument.Parse(Payload).RootElement.Clone());
    }
}
