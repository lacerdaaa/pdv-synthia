using System.Text.Json;
using Synthia.Contracts;
using Synthia.Contracts.Sync;
using Synthia.Terminal.Core.Storage;

namespace Synthia.Terminal.Core.Sync;

/// <summary>
/// Última cópia do cadastro da loja recebida do servidor: dados em <c>meta</c>, logo num arquivo ao lado do banco.
/// Sem cópia (caixa novo, nunca sincronizou), vale o que está no terminal.json.
/// </summary>
public sealed class StoreProfileCache
{
    private readonly LocalDb _db;
    private readonly StoreSection _fallback;

    public StoreProfileCache(LocalDb db, StoreSection fallback, string logoPath)
    {
        _db = db;
        _fallback = fallback;
        LogoPath = logoPath;
        using var conn = db.Open();
        Profile = Meta.Get(conn, Meta.StoreProfile) is { } json ? JsonSerializer.Deserialize<StoreProfile>(json, SynthiaJson.Options) : null;
    }

    public StoreProfile? Profile { get; private set; }

    /// <summary>Onde a logo baixada fica. Use <see cref="HasLogo"/> antes de abrir.</summary>
    public string LogoPath { get; }

    public bool HasLogo => Profile?.LogoHash is not null && File.Exists(LogoPath);

    public event EventHandler? Changed;

    /// <summary>Dados do cupom: cadastro da loja por cima do terminal.json, mas o CNPJ é sempre o do caixa (certificado).</summary>
    public StoreSection Receipt => Profile is not { } p ? _fallback : _fallback with
    {
        Name = NonEmpty(p.TradeName, _fallback.Name),
        Address = NonEmpty(p.Address, _fallback.Address),
        StateRegistration = NonEmpty(p.StateRegistration, _fallback.StateRegistration),
        ReceiptFooter = NonEmpty(p.ReceiptFooter, _fallback.ReceiptFooter),
    };

    /// <summary>O cadastro da loja tem um CNPJ diferente do configurado no caixa: alguém trocou o certificado errado.</summary>
    public bool CnpjMismatch => Profile is { Cnpj.Length: > 0 } p && p.Cnpj != Cnpj.Digits(_fallback.Cnpj);

    /// <summary>Grava a nova cópia. <paramref name="logo"/> nulo com <c>LogoHash</c> preenchido mantém a logo já baixada.</summary>
    public void Apply(StoreProfile profile, byte[]? logo)
    {
        if (profile.LogoHash is null) File.Delete(LogoPath);
        else if (logo is not null)
        {
            var temp = LogoPath + ".tmp";
            File.WriteAllBytes(temp, logo);
            File.Move(temp, LogoPath, overwrite: true);
        }

        using (var conn = _db.Open())
            Meta.Set(conn, Meta.StoreProfile, JsonSerializer.Serialize(profile, SynthiaJson.Options));
        Profile = profile;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string NonEmpty(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
