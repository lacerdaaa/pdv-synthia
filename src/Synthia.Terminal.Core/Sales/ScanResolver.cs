using Synthia.Contracts.Events;
using Synthia.Terminal.Core.Catalog;

namespace Synthia.Terminal.Core.Sales;

/// <param name="FixedTotalCents">Total vindo da etiqueta da balança; nulo quando o caixa calcula.</param>
public sealed record ScanResult(CatalogItem Product, decimal Quantity, long? FixedTotalCents);

/// <summary>
/// Transforma o que o leitor leu em item de carrinho. Primeiro procura o código exato no cadastro
/// (há lojas que usam o prefixo 2 também para códigos internos); senão, tenta como etiqueta de balança.
/// </summary>
public sealed class ScanResolver(LocalCatalog catalog, ScaleBarcodeFormat format)
{
    public ScanResult? Resolve(string code)
    {
        code = code.Trim();
        if (catalog.FindByBarcode(code) is { } product)
            return new ScanResult(product, 1, null);

        if (!ScaleBarcode.TryParse(code, format, out var label) || catalog.FindByPlu(label.Plu) is not { } weighed)
            return null;

        if (label.WeightKg is { } kg)
            return new ScanResult(weighed, kg, null);

        // Etiqueta com total: a quantidade é derivada só para o estoque; o valor cobrado é o da etiqueta.
        var total = label.TotalCents!.Value;
        var quantity = weighed.PriceCents > 0 ? Math.Round((decimal)total / weighed.PriceCents, 3) : 0;
        return new ScanResult(weighed, quantity > 0 ? quantity : 0.001m, total);
    }

    public SaleItem? AddTo(Cart cart, string code) =>
        Resolve(code) is { } r ? cart.Add(r.Product, r.Quantity, r.FixedTotalCents) : null;
}
