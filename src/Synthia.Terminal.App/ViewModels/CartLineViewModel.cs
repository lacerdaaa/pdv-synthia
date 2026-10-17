using Synthia.Contracts.Events;
using Synthia.Terminal.Core;

namespace Synthia.Terminal.App.ViewModels;

public sealed record CartLineViewModel(int Number, SaleItem Item)
{
    public string Index => Number.ToString("000");
    public string Description => Item.Description;
    public string Quantity => Formatting.Quantity(Item.Quantity);
    public string UnitPrice => Formatting.Amount(Item.UnitPriceCents);
    public string Total => Formatting.Amount(Item.TotalCents);
}
