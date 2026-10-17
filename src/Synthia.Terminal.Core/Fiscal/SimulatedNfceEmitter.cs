using Synthia.Contracts.Events;

namespace Synthia.Terminal.Core.Fiscal;

/// <summary>
/// Emissor de desenvolvimento: gera chave válida com a numeração real do caixa, mas não fala com a SEFAZ.
/// Com <see cref="Online"/> falso, emite como contingência offline (tpEmis 9), como a ACBr fará.
/// </summary>
public sealed class SimulatedNfceEmitter(NfceNumbering numbering, string cnpj, TimeProvider clock) : INfceEmitter
{
    public bool Online { get; set; } = true;

    public Task<NfceResult> EmitAsync(SaleCompleted sale, CancellationToken ct = default)
    {
        var key = NfceKey.Create(clock.GetUtcNow(), cnpj, numbering.Serie, numbering.Next(), !Online, Random.Shared.Next(100_000_000));
        return Task.FromResult(new NfceResult(key.Value, Online ? NfceStatus.Authorized : NfceStatus.Pending));
    }
}
