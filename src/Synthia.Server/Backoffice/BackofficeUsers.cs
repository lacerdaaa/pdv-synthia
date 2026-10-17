using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace Synthia.Server.Backoffice;

public sealed record BackofficeUser(Guid Id, string Username, string DisplayName, string Role);

public sealed class BackofficeUsers(NpgsqlDataSource dataSource, IConfiguration config, ILogger<BackofficeUsers> logger)
{
    private static readonly PasswordHasher<BackofficeUser> Hasher = new();

    public async Task<BackofficeUser?> VerifyAsync(string username, string password, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<(Guid Id, string Username, string DisplayName, string Role, string Hash)>(
            "SELECT id, username, display_name, role, password_hash FROM backoffice_users WHERE lower(username) = lower(@username) AND active",
            new { username = username.Trim() });
        if (row.Id == Guid.Empty) return null;

        var user = new BackofficeUser(row.Id, row.Username, row.DisplayName, row.Role);
        var result = Hasher.VerifyHashedPassword(user, row.Hash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            await SetPasswordAsync(user.Id, password, ct);
        return user;
    }

    public async Task<BackofficeUser> CreateAsync(string username, string displayName, string password, string role = "gerente", CancellationToken ct = default)
    {
        if (password.Length < 6) throw new ArgumentException("Senha precisa de pelo menos 6 caracteres.");
        var user = new BackofficeUser(Guid.CreateVersion7(), username.Trim(), displayName.Trim(), role);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO backoffice_users (id, username, display_name, password_hash, role) VALUES (@Id, @Username, @DisplayName, @hash, @Role)",
            new { user.Id, user.Username, user.DisplayName, user.Role, hash = Hasher.HashPassword(user, password) });
        return user;
    }

    public async Task SetPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        if (password.Length < 6) throw new ArgumentException("Senha precisa de pelo menos 6 caracteres.");
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync("UPDATE backoffice_users SET password_hash = @hash WHERE id = @userId",
            new { userId, hash = Hasher.HashPassword(null!, password) });
    }

    /// <summary>
    /// Primeiro start: cria o usuário admin. A senha vem de Backoffice:InitialAdminPassword; sem ela,
    /// é gerada e mostrada uma única vez no log. Nunca existe senha padrão fixa.
    /// </summary>
    public async Task EnsureAdminAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM backoffice_users)")) return;

        var configured = config["Backoffice:InitialAdminPassword"];
        var password = string.IsNullOrWhiteSpace(configured) ? RandomNumberGenerator.GetString("abcdefghjkmnpqrstuvwxyz23456789", 12) : configured;
        await CreateAsync("admin", "Administrador", password, ct: ct);

        if (string.IsNullOrWhiteSpace(configured))
            logger.LogWarning("Retaguarda: usuário 'admin' criado com a senha {Password}. Troque após o primeiro acesso.", password);
        else
            logger.LogInformation("Retaguarda: usuário 'admin' criado com a senha configurada.");
    }
}
