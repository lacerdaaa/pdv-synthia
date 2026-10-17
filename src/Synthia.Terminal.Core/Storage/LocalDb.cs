using Dapper;
using Microsoft.Data.Sqlite;

namespace Synthia.Terminal.Core.Storage;

/// <summary>Banco SQLite do caixa: catálogo, outbox e estado local. Tudo que a venda precisa.</summary>
public sealed class LocalDb
{
    private readonly string _connectionString;

    public LocalDb(TerminalOptions options)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.DatabasePath,
            Pooling = true,
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        conn.Execute("PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;");
        return conn;
    }

    public void EnsureCreated()
    {
        using var conn = Open();
        // WAL: o worker de sync lê a outbox sem bloquear a venda que está escrevendo.
        conn.Execute("PRAGMA journal_mode = WAL;");
        conn.Execute(Schema);
    }

    /// <summary>Datas em texto ISO-8601 UTC: ordenam corretamente como string.</summary>
    public static string ToDbTime(DateTimeOffset value) => value.UtcDateTime.ToString("O");

    public static DateTimeOffset FromDbTime(string value) =>
        DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS meta (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS products (
            id          TEXT    PRIMARY KEY,
            name        TEXT    NOT NULL,
            barcode     TEXT    NULL,
            plu         INTEGER NULL,
            unit        TEXT    NOT NULL,
            price_cents INTEGER NOT NULL,
            ncm         TEXT    NOT NULL,
            active      INTEGER NOT NULL,
            version     INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_products_barcode ON products (barcode);
        CREATE INDEX IF NOT EXISTS ix_products_plu ON products (plu);

        CREATE TABLE IF NOT EXISTS scheduled_prices (
            id           TEXT    PRIMARY KEY,
            product_id   TEXT    NOT NULL,
            price_cents  INTEGER NOT NULL,
            effective_at TEXT    NOT NULL,
            version      INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_scheduled_prices_product ON scheduled_prices (product_id, effective_at);

        CREATE TABLE IF NOT EXISTS outbox (
            event_id        TEXT    PRIMARY KEY,
            stream_id       TEXT    NOT NULL,
            stream_seq      INTEGER NOT NULL,
            type            TEXT    NOT NULL,
            terminal_id     TEXT    NOT NULL,
            occurred_at     TEXT    NOT NULL,
            catalog_version INTEGER NOT NULL,
            payload         TEXT    NOT NULL,
            sent_at         TEXT    NULL
        );
        CREATE INDEX IF NOT EXISTS ix_outbox_pending ON outbox (sent_at, event_id);
        CREATE INDEX IF NOT EXISTS ix_outbox_stream ON outbox (stream_id, stream_seq);
        """;
}
