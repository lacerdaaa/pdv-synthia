using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;

namespace Synthia.Server.Backoffice;

public sealed record ReceiptImportRow(int Line, string Code, StockReceiptItem? Item, string? Error);

/// <summary>
/// Itens de entrada por planilha (romaneio da transferência da loja antiga, lista de conferência...).
/// Colunas: codigo (código de barras ou PLU), quantidade e, opcionalmente, custo.
/// Códigos repetidos são somados numa linha só.
/// </summary>
public static class ReceiptImport
{
    public const string Template = "codigo;quantidade;custo\n7891000100103;24;\n1001;12,5;\n";

    public static IReadOnlyList<ReceiptImportRow> Plan(string csv, IReadOnlyList<ProductData> products)
    {
        var (header, rows) = CsvText.Parse(csv);
        var iCode = Array.FindIndex(header, h => h is "codigo" or "codigo_barras" or "ean" or "plu" or "codigo_de_barras");
        var iQty = Array.FindIndex(header, h => h is "quantidade" or "qtd" or "qtde" or "quant");
        var iCost = Array.FindIndex(header, h => h is "custo" or "custo_unitario" or "valor_unitario");
        if (iCode < 0 || iQty < 0)
            return [new ReceiptImportRow(1, "", null, "Cabeçalho precisa de codigo e quantidade.")];

        var result = new List<ReceiptImportRow>();
        var index = new Dictionary<Guid, int>();

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            string Get(int i) => i >= 0 && i < row.Length ? row[i].Trim() : "";
            var line = r + 2;
            var code = Get(iCode);

            var product = FindProduct(products, code);
            if (product is null) { result.Add(new ReceiptImportRow(line, code, null, $"produto não encontrado: {code}")); continue; }
            if (!CsvText.TryDecimal(Get(iQty), out var qty) || qty <= 0) { result.Add(new ReceiptImportRow(line, code, null, $"quantidade inválida: \"{Get(iQty)}\"")); continue; }
            if (product.Unit != "KG" && qty != decimal.Truncate(qty)) { result.Add(new ReceiptImportRow(line, code, null, $"{product.Name} é por unidade; quantidade fracionada")); continue; }
            long? cost = CsvText.TryMoney(Get(iCost), out var c) ? c : null;

            if (index.TryGetValue(product.Id, out var at))
            {
                var prev = result[at].Item!;
                result[at] = result[at] with { Item = prev with { Quantity = prev.Quantity + qty, UnitCostCents = cost ?? prev.UnitCostCents } };
                continue;
            }

            index[product.Id] = result.Count;
            result.Add(new ReceiptImportRow(line, code, new StockReceiptItem(product.Id, product.Name, qty, cost), null));
        }

        return result;
    }

    /// <summary>Código de barras exato; senão PLU (até 6 dígitos).</summary>
    public static ProductData? FindProduct(IReadOnlyList<ProductData> products, string code)
    {
        code = code.Trim();
        if (code.Length == 0) return null;
        return products.FirstOrDefault(p => p.Barcode == code)
               ?? (code.Length <= 6 && int.TryParse(code, out var plu) ? products.FirstOrDefault(p => p.Plu == plu) : null);
    }
}
