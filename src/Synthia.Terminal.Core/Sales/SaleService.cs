using Dapper;
using Synthia.Contracts;
using Synthia.Contracts.Events;
using Synthia.Terminal.Core.Fiscal;
using Synthia.Terminal.Core.Storage;

namespace Synthia.Terminal.Core.Sales;

public sealed class SaleService(LocalDb db, Outbox outbox, IOutboxSignal signal, INfceEmitter nfce)
{
    public async Task<SaleCompleted> CompleteAsync(Cart cart, IReadOnlyList<SalePayment> payments, CancellationToken ct = default)
    {
        if (cart.Items.Count == 0) throw new InvalidOperationException("Carrinho vazio.");

        var total = cart.TotalCents;
        var paid = payments.Sum(p => p.AmountCents);
        var cash = payments.Where(p => p.Method == PaymentMethods.Cash).Sum(p => p.AmountCents);
        var change = paid - total;

        if (change < 0) throw new InvalidOperationException("Pagamento menor que o total.");
        if (change > cash) throw new InvalidOperationException("Troco só pode sair de pagamento em dinheiro.");

        Guid shiftId;
        using (var conn = db.Open())
        using (var tx = conn.BeginTransaction())
            shiftId = ShiftService.RequireOpenShift(conn, tx);

        var sale = new SaleCompleted(Guid.CreateVersion7(), shiftId, [.. cart.Items], payments, total, change, null, null);

        // A NFC-e vem antes da gravação porque a chave e o status vão dentro do evento.
        var fiscal = await nfce.EmitAsync(sale, ct);
        sale = sale with { NfceKey = fiscal.Key, NfceStatus = fiscal.Status };

        using (var conn = db.Open())
        using (var tx = conn.BeginTransaction())
        {
            outbox.Append(conn, tx, EventTypes.SaleCompleted, sale.SaleId, sale);
            tx.Commit();
        }

        signal.Notify();
        return sale;
    }

    /// <param name="refundMethod">Como o valor volta ao cliente; precisa ser uma forma usada na venda.</param>
    public void CancelItem(Guid saleId, Guid itemId, string reason, string refundMethod)
    {
        var sale = FindSale(saleId) ?? throw new InvalidOperationException("Venda não encontrada neste caixa.");
        if (sale.Items.All(i => i.ItemId != itemId)) throw new InvalidOperationException("Item não pertence à venda.");
        if (sale.Payments.All(p => p.Method != refundMethod))
            throw new InvalidOperationException("Devolução só pela forma de pagamento usada na venda.");

        Record(EventTypes.SaleItemCancelled, saleId, new SaleItemCancelled(saleId, itemId, reason, refundMethod));
    }

    public void CancelSale(Guid saleId, string reason)
    {
        _ = FindSale(saleId) ?? throw new InvalidOperationException("Venda não encontrada neste caixa.");
        Record(EventTypes.SaleCancelled, saleId, new SaleCancelled(saleId, reason));
    }

    public SaleCompleted? FindSale(Guid saleId)
    {
        using var conn = db.Open();
        var json = conn.QuerySingleOrDefault<string>(
            "SELECT payload FROM outbox WHERE stream_id = @saleId AND type = @type",
            new { saleId, type = EventTypes.SaleCompleted });
        return json is null ? null : System.Text.Json.JsonSerializer.Deserialize<SaleCompleted>(json, SynthiaJson.Options);
    }

    private void Record<T>(string type, Guid saleId, T payload)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        outbox.Append(conn, tx, type, saleId, payload);
        tx.Commit();
        signal.Notify();
    }
}
