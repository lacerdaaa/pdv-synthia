using Synthia.Contracts.Events;

namespace Synthia.Terminal.Core.Fiscal;

/// <param name="Status">Um dos valores de <see cref="NfceStatus"/>.</param>
public sealed record NfceResult(string? Key, string Status);

/// <summary>
/// Emissão de NFC-e pelo próprio caixa (ACBr, série própria, certificado A1 local).
/// Sem internet a implementação deve emitir em contingência (tpEmis 9) e devolver
/// <see cref="NfceStatus.Pending"/>; nunca deve bloquear a venda.
/// </summary>
public interface INfceEmitter
{
    Task<NfceResult> EmitAsync(SaleCompleted sale, CancellationToken ct = default);
}
