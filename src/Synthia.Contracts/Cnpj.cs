namespace Synthia.Contracts;

/// <summary>CNPJ: só dígitos para gravar e comparar, máscara para mostrar.</summary>
public static class Cnpj
{
    private static readonly int[] FirstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static string Digits(string? value) => new((value ?? "").Where(char.IsAsciiDigit).ToArray());

    /// <summary>14 dígitos com os dois dígitos verificadores corretos.</summary>
    public static bool IsValid(string? value)
    {
        var d = Digits(value);
        if (d.Length != 14 || d.All(c => c == d[0])) return false;
        return d[12] - '0' == Check(d, FirstWeights) && d[13] - '0' == Check(d, SecondWeights);
    }

    public static string Format(string? value)
    {
        var d = Digits(value);
        return d.Length == 14 ? $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}" : value ?? "";
    }

    private static int Check(string digits, int[] weights)
    {
        var sum = weights.Select((w, i) => (digits[i] - '0') * w).Sum();
        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }
}
