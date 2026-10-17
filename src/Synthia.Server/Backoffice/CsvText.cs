using System.Globalization;
using System.Text;

namespace Synthia.Server.Backoffice;

/// <summary>Leitura de planilhas CSV como o Excel em português salva: ";" e vírgula decimal.</summary>
public static class CsvText
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    static CsvText() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>UTF-8 quando válido; senão Windows-1252 (padrão do "CSV" do Excel no Windows).</summary>
    public static string Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    /// <summary>Linhas e colunas; o separador (";" ou ",") é detectado no cabeçalho. Aceita campos entre aspas.</summary>
    public static (string[] Header, List<string[]> Rows) Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0) return ([], []);

        var sep = lines[0].Count(c => c == ';') >= lines[0].Count(c => c == ',') ? ';' : ',';
        var header = Split(lines[0], sep).Select(Normalize).ToArray();
        return (header, lines.Skip(1).Select(l => Split(l, sep)).ToList());
    }

    public static bool TryDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().Replace("R$", "").Trim();
        // "1.234,56" (pt-BR) ou "1234.56": a última pontuação é a decimal.
        var lastComma = text.LastIndexOf(',');
        var lastDot = text.LastIndexOf('.');
        if (lastDot > lastComma) text = text.Replace(",", "");
        else text = text.Replace(".", "").Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryMoney(string? text, out long cents)
    {
        cents = 0;
        if (!TryDecimal(text, out var v) || v < 0) return false;
        cents = (long)Math.Round(v * 100, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>Cabeçalho sem acento, minúsculo e com "_" — "Código de barras" vira "codigo_de_barras".</summary>
    public static string Normalize(string header)
    {
        var decomposed = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return sb.ToString().Trim('_');
    }

    private static string[] Split(string line, char sep)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == sep && !quoted) { fields.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString().Trim());
        return [.. fields];
    }
}
