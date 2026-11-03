using Dapper;
using Npgsql;

namespace Synthia.Server.Data;

/// <summary>Aplica, em ordem e uma única vez, os scripts SQL embutidos em Data/Migrations.</summary>
public sealed class Migrator(NpgsqlDataSource dataSource, ILogger<Migrator> logger)
{
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Impede dois processos migrando ao mesmo tempo.
        await conn.ExecuteAsync("SELECT pg_advisory_xact_lock(7231001)", transaction: tx);
        await conn.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS schema_migrations (name text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())",
            transaction: tx);

        var applied = (await conn.QueryAsync<string>("SELECT name FROM schema_migrations", transaction: tx)).ToHashSet();

        foreach (var (name, sql) in LoadScripts())
        {
            if (applied.Contains(name)) continue;

            logger.LogInformation("Aplicando migração {Migration}", name);
            // Sem timeout: migração com carga de dados num banco de anos de loja passa bem dos 30 s padrão.
            await conn.ExecuteAsync(sql, transaction: tx, commandTimeout: 0);
            await conn.ExecuteAsync("INSERT INTO schema_migrations (name) VALUES (@name)", new { name }, tx);
        }

        await tx.CommitAsync(ct);
    }

    private static IEnumerable<(string Name, string Sql)> LoadScripts()
    {
        var assembly = typeof(Migrator).Assembly;
        const string prefix = "Synthia.Server.Data.Migrations.";

        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(r => r.StartsWith(prefix) && r.EndsWith(".sql"))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            yield return (resource[prefix.Length..], reader.ReadToEnd());
        }
    }
}
