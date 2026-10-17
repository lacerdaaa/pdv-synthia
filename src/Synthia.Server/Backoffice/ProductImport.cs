using Synthia.Contracts.Catalog;

namespace Synthia.Server.Backoffice;

public enum ImportAction { Create, Update, Unchanged, Error }

public sealed record ProductImportRow(int Line, ImportAction Action, ProductData? Product, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, string Label);

/// <summary>
/// Prévia da importação de produtos por planilha. Colunas reconhecidas (em qualquer ordem):
/// codigo_barras/ean, plu, descricao/nome, unidade, preco, ncm, ativo. Produtos existentes são
/// encontrados pelo código de barras ou pelo PLU e atualizados; os demais são criados.
/// </summary>
public static class ProductImport
{
    public const string Template =
        "codigo_barras;plu;descricao;unidade;preco;ncm\n" +
        "7891000100103;;Arroz Tipo 1 5kg;UN;28,99;10063021\n" +
        ";1001;Banana Prata;KG;5,99;08039000\n";

    private static readonly string[] BarcodeCols = ["codigo_barras", "codigo_de_barras", "ean", "gtin", "barcode", "codigo"];
    private static readonly string[] PluCols = ["plu", "codigo_balanca"];
    private static readonly string[] NameCols = ["descricao", "nome", "produto"];
    private static readonly string[] UnitCols = ["unidade", "un", "unid"];
    private static readonly string[] PriceCols = ["preco", "preco_venda", "valor", "preco_de_venda"];
    private static readonly string[] NcmCols = ["ncm"];
    private static readonly string[] ActiveCols = ["ativo"];

    public static IReadOnlyList<ProductImportRow> Plan(string csv, IReadOnlyList<ProductData> existing)
    {
        var (header, rows) = CsvText.Parse(csv);
        int Col(string[] names) => Array.FindIndex(header, h => names.Contains(h));
        var (iBarcode, iPlu, iName, iUnit, iPrice, iNcm, iActive) =
            (Col(BarcodeCols), Col(PluCols), Col(NameCols), Col(UnitCols), Col(PriceCols), Col(NcmCols), Col(ActiveCols));

        if (iName < 0 || iPrice < 0 || (iBarcode < 0 && iPlu < 0))
            return [new ProductImportRow(1, ImportAction.Error, null,
                ["Cabeçalho precisa de descricao, preco e codigo_barras ou plu."], [], "cabeçalho")];

        var byBarcode = existing.Where(p => p.Barcode is not null).GroupBy(p => p.Barcode!).ToDictionary(g => g.Key, g => g.First());
        var byPlu = existing.Where(p => p.Plu is not null).GroupBy(p => p.Plu!.Value).ToDictionary(g => g.Key, g => g.First());
        var seenBarcodes = new HashSet<string>();
        var seenPlus = new HashSet<int>();
        var result = new List<ProductImportRow>();

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            string Get(int i) => i >= 0 && i < row.Length ? row[i].Trim() : "";
            var line = r + 2;
            var errors = new List<string>();
            var warnings = new List<string>();

            var name = Get(iName);
            var barcode = new string(Get(iBarcode).Where(char.IsAsciiDigit).ToArray());
            var pluText = Get(iPlu);
            int? plu = int.TryParse(pluText, out var p) && p > 0 ? p : null;
            var unit = Get(iUnit).ToUpperInvariant();
            var ncm = new string(Get(iNcm).Where(char.IsAsciiDigit).ToArray());
            var activeText = Get(iActive).ToLowerInvariant();

            if (name.Length == 0) errors.Add("descrição vazia");
            if (!CsvText.TryMoney(Get(iPrice), out var price)) errors.Add($"preço inválido: \"{Get(iPrice)}\"");
            if (barcode.Length == 0 && plu is null) errors.Add("informe código de barras ou PLU");
            if (pluText.Length > 0 && plu is null) errors.Add($"PLU inválido: \"{pluText}\"");
            if (barcode.Length > 0 && !Gtin.IsValid(barcode)) warnings.Add("código de barras com dígito verificador inválido (ok se for código interno)");
            if (ncm.Length != 8) warnings.Add("NCM ausente ou fora de 8 dígitos (obrigatório para NFC-e)");
            if (barcode.Length > 0 && !seenBarcodes.Add(barcode)) errors.Add($"código de barras repetido na planilha: {barcode}");
            if (plu is { } pp && !seenPlus.Add(pp)) errors.Add($"PLU repetido na planilha: {pp}");

            if (unit.Length == 0) unit = plu is not null && barcode.Length == 0 ? "KG" : "UN";
            if (unit is not ("UN" or "KG" or "CX" or "PCT" or "L" or "M")) warnings.Add($"unidade incomum: {unit}");

            var label = name.Length > 0 ? name : $"linha {line}";
            if (errors.Count > 0)
            {
                result.Add(new ProductImportRow(line, ImportAction.Error, null, errors, warnings, label));
                continue;
            }

            var match = (barcode.Length > 0 && byBarcode.TryGetValue(barcode, out var b) ? b : null)
                        ?? (plu is { } k && byPlu.TryGetValue(k, out var q) ? q : null);
            var active = activeText is "" or "sim" or "s" or "1" or "true" or "ativo";
            var product = new ProductData(match?.Id ?? Guid.CreateVersion7(), name, barcode.Length > 0 ? barcode : null, plu, unit, price,
                ncm.Length > 0 ? ncm : "00000000", active);

            var action = match is null ? ImportAction.Create : match == product ? ImportAction.Unchanged : ImportAction.Update;
            result.Add(new ProductImportRow(line, action, product, errors, warnings, label));
        }

        return result;
    }
}

/// <summary>Dígito verificador de GTIN-8/12/13/14.</summary>
public static class Gtin
{
    public static bool IsValid(string code)
    {
        if (code.Length is not (8 or 12 or 13 or 14) || !code.All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var i = code.Length - 2; i >= 0; i--)
            sum += (code[i] - '0') * ((code.Length - 2 - i) % 2 == 0 ? 3 : 1);
        return (10 - sum % 10) % 10 == code[^1] - '0';
    }
}
