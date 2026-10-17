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

    public async Task<EventEnvelope> AppendAsync<T>(string type, Guid streamId, T payload, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var catalogVersion = await conn.ExecuteScalarAsync<long>("SELECT coalesce(max(version), 0) FROM catalog_changes");
        var seq = await conn.ExecuteScalarAsync<int>(
            "SELECT coalesce(max(stream_seq), 0) + 1 FROM events WHERE stream_id = @streamId", new { streamId });

        var now = clock.GetUtcNow();
        var envelope = new EventEnvelope(Uuid7.Next(now), streamId, seq, type, TerminalId, now, catalogVersion, SynthiaJson.ToElement(payload));
        await store.AppendAsync([envelope], ct);
        return envelope;
    }
}
