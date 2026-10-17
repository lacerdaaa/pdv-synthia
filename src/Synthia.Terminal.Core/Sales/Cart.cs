using Synthia.Contracts.Events;
using Synthia.Terminal.Core.Catalog;

namespace Synthia.Terminal.Core.Sales;

/// <summary>
/// Venda em andamento, só em memória. Remover item aqui não gera evento: nada foi vendido ainda.
/// O preço é congelado no momento em que o item entra no carrinho.
/// </summary>
public sealed class Cart
{
    private readonly List<SaleItem> _items = [];

    public IReadOnlyList<SaleItem> Items => _items;

    public long TotalCents => _items.Sum(i => i.TotalCents);

    public SaleItem Add(CatalogItem product, decimal quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

        var total = (long)Math.Round(quantity * product.PriceCents, MidpointRounding.AwayFromZero);
        var item = new SaleItem(Guid.CreateVersion7(), product.Id, product.Name, quantity, product.PriceCents, total);
        _items.Add(item);
        return item;
    }

    public bool Remove(Guid itemId) => _items.RemoveAll(i => i.ItemId == itemId) > 0;
}
