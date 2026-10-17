using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace Synthia.Server.Backoffice;

public sealed record BackofficeUser(Guid Id, string Username, string DisplayName, string Role);

public sealed record BackofficeUserRow(Guid Id, string Username, string DisplayName, string Role, bool Active, DateTime CreatedAt);

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

    public async Task<BackofficeUser> CreateAsync(string username, string displayName, string password, string role = BackofficeRoles.Manager, CancellationToken ct = default)
    {
        if (password.Length < 6) throw new ArgumentException("Senha precisa de pelo menos 6 caracteres.");
        if (!BackofficeRoles.All.Contains(role)) throw new ArgumentException("Papel inválido.");
        if (string.IsNullOrWhiteSpace(username) || username.Any(char.IsWhiteSpace)) throw new ArgumentException("Usuário sem espaços.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Informe o nome.");
        var user = new BackofficeUser(Guid.CreateVersion7(), username.Trim(), displayName.Trim(), role);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM backoffice_users WHERE lower(username) = lower(@u))", new { u = user.Username }))
            throw new ArgumentException("Já existe usuário com esse nome.");
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

    public async Task<IReadOnlyList<BackofficeUserRow>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<BackofficeUserRow>(
            "SELECT id, username, display_name AS DisplayName, role, active, created_at AS CreatedAt FROM backoffice_users ORDER BY active DESC, display_name")).ToList();
    }

    /// <summary>Revalidação do cookie a cada requisição: usuário desativado perde o acesso na hora.</summary>
    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>("SELECT coalesce((SELECT active FROM backoffice_users WHERE id = @userId), false)", new { userId });
    }

    public async Task ChangeOwnPasswordAsync(Guid userId, string current, string next, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<(string Username, string Hash)>(
            "SELECT username, password_hash FROM backoffice_users WHERE id = @userId AND active", new { userId });
        if (row.Hash is null || Hasher.VerifyHashedPassword(null!, row.Hash, current) == PasswordVerificationResult.Failed)
            throw new ArgumentException("Senha atual incorreta.");
        await SetPasswordAsync(userId, next, ct);
    }

    public async Task SetRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        if (!BackofficeRoles.All.Contains(role)) throw new ArgumentException("Papel inválido.");
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync("UPDATE backoffice_users SET role = @role WHERE id = @userId", new { userId, role }, tx);
        await EnsureAnAdminRemainsAsync(conn, tx);
        await tx.CommitAsync(ct);
    }

    public async Task SetActiveAsync(Guid userId, bool active, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync("UPDATE backoffice_users SET active = @active WHERE id = @userId", new { userId, active }, tx);
        await EnsureAnAdminRemainsAsync(conn, tx);
        await tx.CommitAsync(ct);
    }

    private static async Task EnsureAnAdminRemainsAsync(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        var admins = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM backoffice_users WHERE role = @Admin AND active", new { BackofficeRoles.Admin }, tx);
        if (admins == 0) throw new ArgumentException("A retaguarda precisa de pelo menos um administrador ativo.");
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
        await CreateAsync("admin", "Administrador", password, BackofficeRoles.Admin, ct);

        if (string.IsNullOrWhiteSpace(configured))
            logger.LogWarning("Retaguarda: usuário 'admin' criado com a senha {Password}. Troque após o primeiro acesso.", password);
        else
            logger.LogInformation("Retaguarda: usuário 'admin' criado com a senha configurada.");
    }
}
