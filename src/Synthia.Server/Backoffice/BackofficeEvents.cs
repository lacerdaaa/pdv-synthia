using Dapper;
using Npgsql;
using Synthia.Contracts;
using Synthia.Contracts.Events;
using Synthia.Server.Sync;

namespace Synthia.Server.Backoffice;

/// <summary>
/// Fatos que nascem na retaguarda (entradas, contagens, ajustes). Entram no mesmo log de eventos dos
/// caixas, com origem <see cref="TerminalId"/>, e passam pelas mesmas projeções.
/// </summary>
public sealed class BackofficeEvents(EventStore store, NpgsqlDataSource dataSource, TimeProvider clock)
{
    public const string TerminalId = "RETAGUARDA";

    /// <param name="occurredAt">Quando o fato aconteceu (ex.: hora em que o item foi contado); padrão: agora.</param>
    public async Task<EventEnvelope> AppendAsync<T>(string type, Guid streamId, T payload, CancellationToken ct = default, DateTimeOffset? occurredAt = null) =>
        (await AppendManyAsync(type, [(streamId, payload, occurredAt)], ct))[0];

    /// <summary>Vários fatos do mesmo tipo numa única transação (ex.: todas as contagens de um inventário).</summary>
    public async Task<IReadOnlyList<EventEnvelope>> AppendManyAsync<T>(
        string type, IReadOnlyList<(Guid StreamId, T Payload, DateTimeOffset? OccurredAt)> facts, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var catalogVersion = await conn.ExecuteScalarAsync<long>("SELECT coalesce(max(version), 0) FROM catalog_changes");
        var now = clock.GetUtcNow();
        var envelopes = new List<EventEnvelope>(facts.Count);

        foreach (var (streamId, payload, occurredAt) in facts)
        {
            var seq = await conn.ExecuteScalarAsync<int>(
                "SELECT coalesce(max(stream_seq), 0) + 1 FROM events WHERE stream_id = @streamId", new { streamId });
            seq += envelopes.Count(e => e.StreamId == streamId);
            var at = occurredAt ?? now;
            envelopes.Add(new EventEnvelope(Uuid7.Next(at), streamId, seq, type, TerminalId, at, catalogVersion, SynthiaJson.ToElement(payload)));
        }

        await store.AppendAsync(envelopes, ct);
        return envelopes;
    }
}
