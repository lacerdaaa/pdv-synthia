namespace Synthia.Terminal.Core.Fiscal;

/// <summary>
/// Chave de acesso da NFC-e (44 dígitos): UF + AAMM + CNPJ + modelo 65 + série + número + tpEmis + código + DV.
/// </summary>
public sealed record NfceKey(string Value)
{
    public const string UfSaoPaulo = "35";

    public int Serie => int.Parse(Value.AsSpan(22, 3));
    public long Number => long.Parse(Value.AsSpan(25, 9));

    /// <summary>1 = normal; 9 = contingência offline.</summary>
    public int EmissionType => Value[34] - '0';

    public static NfceKey Create(DateTimeOffset issuedAt, string cnpj, int serie, long number, bool offline, int randomCode)
    {
        var digits = new string(cnpj.Where(char.IsAsciiDigit).ToArray()).PadLeft(14, '0');
        var body = $"{UfSaoPaulo}{issuedAt:yyMM}{digits}65{serie:D3}{number:D9}{(offline ? 9 : 1)}{randomCode % 100_000_000:D8}";
        return new NfceKey(body + CheckDigit(body));
    }

    /// <summary>Módulo 11 com pesos 2 a 9 da direita para a esquerda.</summary>
    public static int CheckDigit(string body)
    {
        var sum = 0;
        var weight = 2;
        for (var i = body.Length - 1; i >= 0; i--)
        {
            sum += (body[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }

        var dv = 11 - sum % 11;
        return dv >= 10 ? 0 : dv;
    }

    public static bool IsValid(string key) =>
        key.Length == 44 && key.All(char.IsAsciiDigit) && CheckDigit(key[..43]) == key[43] - '0';

    /// <summary>Chave em blocos de 4, como no DANFE.</summary>
    public string Formatted => string.Join(' ', Value.Chunk(4).Select(c => new string(c)));
}
