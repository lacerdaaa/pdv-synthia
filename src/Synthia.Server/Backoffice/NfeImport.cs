using Dapper;
using Npgsql;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;

namespace Synthia.Server.Backoffice;

public enum NfeMatch { Mapping, TaxEan, Ean, Manual, None }

/// <param name="Factor">Unidades nossas por unidade comercial da nota.</param>
public sealed record NfeLine(NfeItem Item, ProductData? Product, decimal Factor, NfeMatch MatchedBy, bool Ignored = false)
{
    public decimal Quantity => Math.Round(Item.Quantity * Factor, 3);

    /// <summary>Custo por unidade nossa, em centavos.</summary>
    public long? UnitCostCents => Quantity > 0 ? (long)Math.Round(Item.TotalValue / Quantity * 100, MidpointRounding.AwayFromZero) : null;

    public bool Ready => Ignored || (Product is not null && Factor > 0);
}

public sealed record NfePlan(NfeDocument Document, IReadOnlyList<NfeLine> Lines, bool AlreadyImported)
{
    public bool CanReceive => !AlreadyImported && Lines.All(l => l.Ready) && Lines.Any(l => !l.Ignored);
}

/// <summary>Importação do XML da NF-e do fornecedor como entrada de mercadoria.</summary>
public sealed class NfeImport(NpgsqlDataSource dataSource, StockService stock)
{
    public async Task<NfePlan> PlanAsync(string xml, IReadOnlyList<ProductData> products, CancellationToken ct = default)
    {
        var doc = NfeXml.Parse(xml);
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var mappings = (await conn.QueryAsync<(string Code, Guid ProductId, decimal Factor)>(
                "SELECT supplier_code, product_id, factor FROM supplier_products WHERE supplier_cnpj = @cnpj", new { cnpj = doc.SupplierCnpj }))
            .ToDictionary(m => m.Code);
        var alreadyImported = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM events WHERE type = @type AND payload->>'accessKey' = @key)",
            new { type = EventTypes.StockReceived, key = doc.AccessKey });

        var byId = products.ToDictionary(p => p.Id);
        var byBarcode = products.Where(p => p.Barcode is not null && p.Active).GroupBy(p => p.Barcode!).ToDictionary(g => g.Key, g => g.First());

        var lines = doc.Items.Select(item =>
        {
            if (mappings.TryGetValue(item.Code, out var m) && byId.TryGetValue(m.ProductId, out var mapped))
                return new NfeLine(item, mapped, m.Factor, NfeMatch.Mapping);
            if (item.TaxEan is { } taxEan && byBarcode.TryGetValue(taxEan, out var byTax) && item.Quantity > 0)
                return new NfeLine(item, byTax, item.TaxQuantity > 0 ? item.TaxQuantity / item.Quantity : 1, NfeMatch.TaxEan);
            if (item.Ean is { } ean && byBarcode.TryGetValue(ean, out var byEan))
                return new NfeLine(item, byEan, 1, NfeMatch.Ean);
            return new NfeLine(item, null, 1, NfeMatch.None);
        }).ToList();

        return new NfePlan(doc, lines, alreadyImported);
    }

    /// <summary>
    /// Lança a nota como entrada e guarda o vínculo de cada item com o nosso produto, para a próxima
    /// nota do mesmo fornecedor entrar sem intervenção.
    /// </summary>
    public async Task<StockReceived> ReceiveAsync(NfePlan plan, string user, CancellationToken ct = default)
    {
        if (plan.AlreadyImported) throw new InvalidOperationException("Esta nota já foi lançada.");
        if (!plan.CanReceive) throw new InvalidOperationException("Há itens sem produto vinculado.");

        var lines = plan.Lines.Where(l => !l.Ignored).ToList();
        await using (var conn = await dataSource.OpenConnectionAsync(ct))
        {
            foreach (var l in lines)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO supplier_products (supplier_cnpj, supplier_code, product_id, factor)
                    VALUES (@cnpj, @code, @productId, @factor)
                    ON CONFLICT (supplier_cnpj, supplier_code) DO UPDATE
                      SET product_id = excluded.product_id, factor = excluded.factor, updated_at = now()
                    """,
                    new { cnpj = plan.Document.SupplierCnpj, code = l.Item.Code, productId = l.Product!.Id, factor = l.Factor });
            }
        }

        // Itens da nota que apontam para o mesmo produto viram uma linha só.
        var items = lines
            .GroupBy(l => l.Product!.Id)
            .Select(g =>
            {
                var qty = g.Sum(l => l.Quantity);
                var cost = qty > 0 ? (long?)Math.Round(g.Sum(l => l.Item.TotalValue) / qty * 100, MidpointRounding.AwayFromZero) : null;
                return new StockReceiptItem(g.Key, g.First().Product!.Name, qty, cost);
            })
            .ToList();

        var d = plan.Document;
        return await stock.ReceiveAsync(StockReceiptSources.Supplier, d.SupplierName, $"NF {d.Number}/{d.Series}", items, user,
            $"XML importado · {lines.Count} item(ns) da nota", d.AccessKey, ct);
    }
}
