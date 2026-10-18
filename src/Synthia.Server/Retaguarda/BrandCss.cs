using System.Globalization;
using Synthia.Contracts.Sync;

namespace Synthia.Server.Retaguarda;

/// <summary>Variáveis de cor da retaguarda a partir das cores cadastradas da loja.</summary>
public static class BrandCss
{
    public static string Render(StoreProfile p) =>
        $":root {{ --navy: {p.PrimaryColor}; --navy-dark: {Darken(p.PrimaryColor, 0.3)}; --red: {p.AccentColor}; }}\n";

    /// <summary>Escurece #RRGGBB pela fração dada (0,3 = 30% mais escuro).</summary>
    public static string Darken(string hex, double amount)
    {
        var rgb = Convert.ToInt32(hex.TrimStart('#'), 16);
        int Channel(int shift) => (int)Math.Round(((rgb >> shift) & 0xFF) * (1 - amount));
        return string.Create(CultureInfo.InvariantCulture, $"#{Channel(16):X2}{Channel(8):X2}{Channel(0):X2}");
    }
}
