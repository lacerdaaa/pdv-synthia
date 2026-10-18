using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using Synthia.Contracts;
using Synthia.Contracts.Sync;

namespace Synthia.Server.Backoffice;

public sealed record StoreProfileInput(
    string TradeName,
    string LegalName,
    string Cnpj,
    string StateRegistration,
    string Address,
    string ReceiptFooter,
    string PrimaryColor,
    string AccentColor);

public sealed record StoreLogo(byte[] Bytes, string ContentType, string Hash);

/// <summary>
/// Cadastro da loja (uma linha em store_profile). Fica em memória: a retaguarda e os caixas leem
/// muito e a loja quase nunca muda; toda alteração passa por aqui e renova a cópia.
/// </summary>
public sealed partial class StoreProfileService(NpgsqlDataSource dataSource)
{
    public const int MaxLogoBytes = 512 * 1024;
    public const int MaxTradeName = 40;

    private const string Columns = """
        store_id AS StoreId, trade_name AS TradeName, legal_name AS LegalName, cnpj, state_registration AS StateRegistration,
        address, receipt_footer AS ReceiptFooter, primary_color AS PrimaryColor, accent_color AS AccentColor,
        left(encode(sha256(logo), 'hex'), 16) AS LogoHash, version
        """;

    private volatile StoreProfile? _cached;

    public async Task<StoreProfile> GetAsync(CancellationToken ct = default)
    {
        if (_cached is { } cached) return cached;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return _cached = (await conn.QuerySingleAsync<Row>($"SELECT {Columns} FROM store_profile")).ToProfile();
    }

    public async Task<StoreLogo?> GetLogoAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<(byte[]? Logo, string? ContentType, string? Hash)>(
            "SELECT logo, logo_content_type, left(encode(sha256(logo), 'hex'), 16) FROM store_profile");
        return row.Logo is null ? null : new StoreLogo(row.Logo, row.ContentType ?? "image/png", row.Hash!);
    }

    public async Task<StoreProfile> UpdateAsync(StoreProfileInput input, CancellationToken ct = default)
    {
        var name = input.TradeName.Trim();
        if (name.Length == 0) throw new ArgumentException("Informe o nome da loja.");
        if (name.Length > MaxTradeName) throw new ArgumentException($"Nome da loja com até {MaxTradeName} caracteres (cabe na linha do cupom).");
        var cnpj = Cnpj.Digits(input.Cnpj);
        if (cnpj.Length > 0 && !Cnpj.IsValid(cnpj)) throw new ArgumentException("CNPJ inválido.");
        if (!HexColor().IsMatch(input.PrimaryColor) || !HexColor().IsMatch(input.AccentColor))
            throw new ArgumentException("Cores no formato #RRGGBB.");

        return await SaveAsync("""
            UPDATE store_profile SET trade_name = @name, legal_name = @LegalName, cnpj = @cnpj, state_registration = @StateRegistration,
                   address = @Address, receipt_footer = @ReceiptFooter, primary_color = @primary, accent_color = @accent,
                   version = version + 1, updated_at = now()
            """, new
        {
            name, cnpj,
            LegalName = input.LegalName.Trim(),
            StateRegistration = input.StateRegistration.Trim(),
            Address = input.Address.Trim(),
            ReceiptFooter = input.ReceiptFooter.Trim(),
            primary = input.PrimaryColor.ToUpperInvariant(),
            accent = input.AccentColor.ToUpperInvariant(),
        }, ct);
    }

    /// <summary>PNG ou JPEG de até 512 KB. O tipo vem do conteúdo, não do nome do arquivo.</summary>
    public async Task<StoreProfile> SetLogoAsync(byte[] bytes, CancellationToken ct = default)
    {
        if (bytes.Length > MaxLogoBytes) throw new ArgumentException("Logo acima de 512 KB.");
        var contentType = ImageType(bytes) ?? throw new ArgumentException("A logo precisa ser PNG ou JPEG.");
        return await SaveAsync(
            "UPDATE store_profile SET logo = @bytes, logo_content_type = @contentType, version = version + 1, updated_at = now()",
            new { bytes, contentType }, ct);
    }

    public Task<StoreProfile> RemoveLogoAsync(CancellationToken ct = default) => SaveAsync(
        "UPDATE store_profile SET logo = NULL, logo_content_type = NULL, version = version + 1, updated_at = now()", null, ct);

    private async Task<StoreProfile> SaveAsync(string sql, object? args, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<Row>($"{sql} RETURNING {Columns}", args);
        return _cached = row.ToProfile();
    }

    private static string? ImageType(byte[] b) =>
        b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 ? "image/png"
        : b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? "image/jpeg"
        : null;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();

    private sealed class Row
    {
        public Guid StoreId { get; init; }
        public string TradeName { get; init; } = "";
        public string LegalName { get; init; } = "";
        public string Cnpj { get; init; } = "";
        public string StateRegistration { get; init; } = "";
        public string Address { get; init; } = "";
        public string ReceiptFooter { get; init; } = "";
        public string PrimaryColor { get; init; } = "";
        public string AccentColor { get; init; } = "";
        public string? LogoHash { get; init; }
        public long Version { get; init; }

        public StoreProfile ToProfile() => new(StoreId, TradeName, LegalName, Cnpj, StateRegistration, Address, ReceiptFooter,
            PrimaryColor, AccentColor, LogoHash, Version);
    }
}
