using Synthia.Contracts;
using Synthia.Contracts.Catalog;
using Synthia.Terminal.Core.Catalog;

namespace Synthia.Terminal.App;

/// <summary>
/// Catálogo de exemplo para o modo demonstração (banco separado, caixa-demo.db). Em produção o
/// catálogo só vem do servidor.
/// </summary>
internal static class DemoCatalog
{
    public static readonly ProductData[] Products =
    [
        new(Guid.Parse("0199f000-0000-7000-8000-000000000001"), "Arroz Tipo 1 5kg", "7891000100103", null, "UN", 2899, "10063021", true),
        new(Guid.Parse("0199f000-0000-7000-8000-000000000002"), "Feijão Carioca 1kg", "7891000200209", null, "UN", 899, "07133399", true),
        new(Guid.Parse("0199f000-0000-7000-8000-000000000003"), "Café Torrado 500g", "7891000400401", null, "UN", 1899, "09012100", true),
        new(Guid.Parse("0199f000-0000-7000-8000-000000000004"), "Leite Integral 1L", "7891000300305", null, "UN", 549, "04012010", true),
        new(Guid.Parse("0199f000-0000-7000-8000-000000000005"), "Azeite Extravirgem 500ml", "7891000500507", null, "UN", 3490, "15091000", true),
        new(Guid.Parse("0199f000-0000-7000-8000-000000000006"), "Refrigerante Cola 2L", "7891000600603", null, "UN", 999, "22021000", true),
        new(Guid.Parse("0199f000-0000-7000-8000-000000000007"), "Banana Prata", null, 1001, "KG", 599, "08039000", true),
        new(Guid.Parse("0199f000-0000-7000-8000-000000000008"), "Pão Francês", null, 1002, "KG", 1690, "19059090", true),
        new(Guid.Parse("0199f000-0000-7000-8000-000000000009"), "Tomate Italiano", null, 1003, "KG", 899, "07020000", true),
    ];

    public static void SeedIfEmpty(LocalCatalog catalog)
    {
        if (catalog.CurrentVersion() > 0) return;
        catalog.ApplyDelta(Products
            .Select((p, i) => new CatalogChange(i + 1, CatalogEntities.Product, p.Id, SynthiaJson.ToElement(p)))
            .ToList());
    }
}
