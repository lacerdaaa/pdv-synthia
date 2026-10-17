namespace Synthia.Terminal.Core.Catalog;

/// <summary>O que a etiqueta da balança embute depois do código do produto.</summary>
public enum ScaleLabelValue
{
    /// <summary>Total a pagar em centavos (padrão mais comum no MGV/Toledo).</summary>
    TotalPrice,

    /// <summary>Peso em gramas.</summary>
    Weight,
}

/// <summary>
/// Layout da etiqueta EAN-13 das balanças etiquetadoras: <c>2 + código + valor + dígito verificador</c>.
/// Precisa bater com a configuração da balança (no MGV: tamanho do código e tipo de valor).
/// </summary>
public sealed record ScaleBarcodeFormat(int CodeLength = 6, int ValueLength = 5, ScaleLabelValue Value = ScaleLabelValue.TotalPrice)
{
    public static readonly ScaleBarcodeFormat Default = new();

    public void Validate()
    {
        if (CodeLength < 4 || ValueLength < 4 || CodeLength + ValueLength != 11)
            throw new ArgumentException("Código + valor precisam somar 11 dígitos (EAN-13 = 2 + código + valor + DV).");
    }
}

/// <param name="Plu">Código do produto na balança (o PLU do cadastro).</param>
/// <param name="TotalCents">Preenchido quando a etiqueta traz o total.</param>
/// <param name="WeightKg">Preenchido quando a etiqueta traz o peso.</param>
public sealed record ScaleLabel(int Plu, long? TotalCents, decimal? WeightKg);

public static class ScaleBarcode
{
    public static bool TryParse(string code, ScaleBarcodeFormat format, out ScaleLabel label)
    {
        label = null!;
        if (code.Length != 13 || code[0] != '2' || !Ean13.IsValid(code)) return false;

        var plu = int.Parse(code.AsSpan(1, format.CodeLength));
        var value = long.Parse(code.AsSpan(1 + format.CodeLength, format.ValueLength));

        label = format.Value == ScaleLabelValue.TotalPrice
            ? new ScaleLabel(plu, value, null)
            : new ScaleLabel(plu, null, value / 1000m);
        return true;
    }
}

public static class Ean13
{
    public static bool IsValid(string code)
    {
        if (code.Length != 13 || !code.All(char.IsAsciiDigit)) return false;

        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (code[i] - '0') * (i % 2 == 0 ? 1 : 3);

        return (10 - sum % 10) % 10 == code[12] - '0';
    }
}
