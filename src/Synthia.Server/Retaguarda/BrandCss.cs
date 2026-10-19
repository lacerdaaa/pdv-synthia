using Synthia.Contracts.Sync;

namespace Synthia.Server.Retaguarda;

/// <summary>
/// Cor da loja na retaguarda. Co-marca: a interface é sempre Synthia; da loja entra só a cor de destaque, na faixa
/// sob a logo (--tenant-accent). A cor principal cadastrada fica guardada, mas não pinta a tela.
/// </summary>
public static class BrandCss
{
    public static string Render(StoreProfile p) => $":root {{ --tenant-accent: {p.AccentColor}; }}\n";
}
