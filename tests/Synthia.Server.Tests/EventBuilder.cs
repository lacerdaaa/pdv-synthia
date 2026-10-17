using Synthia.Contracts;
using Synthia.Contracts.Events;

namespace Synthia.Server.Tests;

internal static class EventBuilder
{
    public static EventEnvelope Make<T>(string type, Guid streamId, int seq, T payload, DateTimeOffset? at = null) =>
        new(Uuid7.Next(), streamId, seq, type, "CX01", at ?? DateTimeOffset.UtcNow, 1, SynthiaJson.ToElement(payload));

    public static (EventEnvelope Event, SaleCompleted Sale) Sale(Guid shiftId, params (Guid ProductId, decimal Qty, long UnitCents)[] items)
    {
        var saleItems = items
            .Select(i => new SaleItem(Guid.CreateVersion7(), i.ProductId, "Produto", i.Qty, i.UnitCents, (long)Math.Round(i.Qty * i.UnitCents)))
            .ToList();
        var total = saleItems.Sum(i => i.TotalCents);
        var sale = new SaleCompleted(Guid.CreateVersion7(), shiftId, saleItems,
            [new SalePayment(PaymentMethods.Cash, total)], total, 0, null, NfceStatus.Authorized);

        return (Make(EventTypes.SaleCompleted, sale.SaleId, 1, sale), sale);
    }
}
