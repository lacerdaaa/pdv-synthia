using System.Globalization;

namespace Synthia.Terminal.Core;

/// <summary>Formatos de tela e de cupom em pt-BR.</summary>
public static class Formatting
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Money(long cents) => (cents / 100m).ToString("C", PtBr);

    public static string Amount(long cents) => (cents / 100m).ToString("N2", PtBr);

    public static string Quantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity) ? quantity.ToString("0", PtBr) : quantity.ToString("0.000", PtBr);

    /// <summary>Lê valores digitados como "70", "70,5" ou "70,50" (vírgula ou ponto).</summary>
    public static bool TryParseMoney(string text, out long cents)
    {
        cents = 0;
        text = text.Trim().Replace("R$", "").Trim().Replace('.', ',');
        if (!decimal.TryParse(text, NumberStyles.Number, PtBr, out var value) || value < 0) return false;
        cents = (long)Math.Round(value * 100, MidpointRounding.AwayFromZero);
        return true;
    }

    public static bool TryParseQuantity(string text, out decimal quantity) =>
        decimal.TryParse(text.Trim().Replace('.', ','), NumberStyles.Number, PtBr, out quantity) && quantity > 0;
}
