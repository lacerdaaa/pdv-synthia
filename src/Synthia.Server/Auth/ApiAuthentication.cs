using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Synthia.Server.Auth;

public static class ApiAuth
{
    public const string TerminalScheme = "Terminal";
    public const string ApiKeyScheme = "ApiKey";
    public const string TerminalPolicy = "Terminal";
    public const string BackofficeApiPolicy = "BackofficeApi";
    public const string TerminalClaim = "synthia:terminal";
    public const string ApiRole = "api";

    /// <summary>Terminal autenticado sem chaves configuradas (só em desenvolvimento): pode enviar por qualquer caixa.</summary>
    public const string AnyTerminal = "*";

    public static bool KeyMatches(string? presented, string? expected) =>
        presented is not null && expected is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected));
}

/// <summary>
/// Caixas: cabeçalhos X-Terminal-Id e X-Terminal-Key, conferidos com Sync:Terminals (ex.: Sync:Terminals:CX01).
/// Sem nenhuma chave configurada, só em desenvolvimento, aceita qualquer caixa para o modo demonstração.
/// </summary>
public sealed class TerminalKeyHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IConfiguration config, IHostEnvironment env)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var keys = config.GetSection("Sync:Terminals").GetChildren().Where(k => !string.IsNullOrWhiteSpace(k.Value)).ToList();
        if (keys.Count == 0)
        {
            if (!env.IsDevelopment()) return Task.FromResult(AuthenticateResult.Fail("Nenhum caixa configurado em Sync:Terminals."));
            Logger.LogWarning("Sync sem chaves de caixa (Sync:Terminals): aceito só porque o ambiente é Development.");
            return Task.FromResult(Success(ApiAuth.AnyTerminal));
        }

        var id = Request.Headers["X-Terminal-Id"].ToString();
        var key = Request.Headers["X-Terminal-Key"].ToString();
        if (id.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());

        var expected = keys.FirstOrDefault(k => string.Equals(k.Key, id, StringComparison.OrdinalIgnoreCase))?.Value;
        return Task.FromResult(ApiAuth.KeyMatches(key, expected) ? Success(id.ToUpperInvariant()) : AuthenticateResult.Fail("Chave de caixa inválida."));
    }

    private AuthenticateResult Success(string terminal)
    {
        var identity = new ClaimsIdentity([new Claim(ApiAuth.TerminalClaim, terminal), new Claim(ClaimTypes.Name, terminal)], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}

/// <summary>Integrações e automação na API do back-office: cabeçalho X-Api-Key contra Backoffice:ApiKeys.</summary>
public sealed class ApiKeyHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = Request.Headers["X-Api-Key"].ToString();
        if (presented.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());

        var valid = config.GetSection("Backoffice:ApiKeys").GetChildren().Any(k => ApiAuth.KeyMatches(presented, k.Value));
        if (!valid) return Task.FromResult(AuthenticateResult.Fail("Chave de API inválida."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "api"), new Claim(ClaimTypes.Role, ApiAuth.ApiRole)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
