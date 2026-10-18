using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Synthia.Terminal.Core.Sync;

/// <summary>
/// A cada minuto confere o cadastro da loja no servidor. Só regrava quando a versão muda e só baixa
/// a logo quando o hash dela muda. Servidor fora do ar: fica com a última cópia.
/// </summary>
public sealed class StoreProfilePuller(StoreProfileCache cache, SyncClient client, ILogger<StoreProfilePuller> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>Devolve verdadeiro quando havia novidade.</summary>
    public async Task<bool> PullAsync(CancellationToken ct = default)
    {
        var profile = await client.GetStoreProfileAsync(ct);
        var current = cache.Profile;
        if (current is not null && current.StoreId == profile.StoreId && current.Version == profile.Version && (profile.LogoHash is null || cache.HasLogo))
            return false;

        var logoChanged = profile.LogoHash is not null && (profile.LogoHash != current?.LogoHash || !cache.HasLogo);
        var logo = logoChanged ? await client.GetStoreLogoAsync(ct) : null;
        cache.Apply(logoChanged && logo is null ? profile with { LogoHash = null } : profile, logo);

        if (cache.CnpjMismatch)
            logger.LogWarning("CNPJ do cadastro da loja ({Store}) difere do configurado neste caixa", profile.Cnpj);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await PullAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Falha ao atualizar os dados da loja; mantendo a última cópia");
            }
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }
}
