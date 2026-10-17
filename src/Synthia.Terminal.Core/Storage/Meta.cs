using System.Data;
using Dapper;

namespace Synthia.Terminal.Core.Storage;

/// <summary>Pares chave/valor do caixa (versão do catálogo, turno aberto...).</summary>
public static class Meta
{
    public const string LastCatalogVersion = "last_catalog_version";
    public const string OpenShiftId = "open_shift_id";

    public static string? Get(IDbConnection conn, string key, IDbTransaction? tx = null) =>
        conn.QuerySingleOrDefault<string>("SELECT value FROM meta WHERE key = @key", new { key }, tx);

    public static void Set(IDbConnection conn, string key, string value, IDbTransaction? tx = null) =>
        conn.Execute(
            "INSERT INTO meta (key, value) VALUES (@key, @value) ON CONFLICT (key) DO UPDATE SET value = excluded.value",
            new { key, value }, tx);

    public static void Delete(IDbConnection conn, string key, IDbTransaction? tx = null) =>
        conn.Execute("DELETE FROM meta WHERE key = @key", new { key }, tx);

    public static long GetCatalogVersion(IDbConnection conn, IDbTransaction? tx = null) =>
        long.TryParse(Get(conn, LastCatalogVersion, tx), out var v) ? v : 0;
}
