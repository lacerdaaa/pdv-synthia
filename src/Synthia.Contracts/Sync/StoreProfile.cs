namespace Synthia.Contracts.Sync;

/// <summary>
/// Identidade da loja cadastrada na retaguarda: nome no cupom e nas telas, dados fiscais para conferência,
/// rodapé do cupom e marca (cores e logo). Os caixas guardam a última cópia e funcionam com ela offline.
/// </summary>
/// <param name="StoreId">Identifica esta loja de forma única; é a chave dela quando houver nuvem com várias lojas.</param>
/// <param name="Cnpj">Só dígitos; vazio enquanto não cadastrado.</param>
/// <param name="LogoHash">Muda quando a logo muda; nulo sem logo. O caixa só baixa a imagem quando o hash muda.</param>
/// <param name="Version">Sobe a cada alteração.</param>
public sealed record StoreProfile(
    Guid StoreId,
    string TradeName,
    string LegalName,
    string Cnpj,
    string StateRegistration,
    string Address,
    string ReceiptFooter,
    string PrimaryColor,
    string AccentColor,
    string? LogoHash,
    long Version);
