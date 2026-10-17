using Synthia.Contracts.Events;
using Synthia.Terminal.Core.Storage;

namespace Synthia.Terminal.Core.Sales;

public sealed class ShiftService(LocalDb db, Outbox outbox, IOutboxSignal signal)
{
    public Guid? CurrentShiftId()
    {
        using var conn = db.Open();
        return Guid.TryParse(Meta.Get(conn, Meta.OpenShiftId), out var id) ? id : null;
    }

    public string? CurrentOperator()
    {
        using var conn = db.Open();
        return Meta.Get(conn, Meta.OpenShiftOperator);
    }

    public Guid Open(string operatorName, long openingCashCents)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        if (Meta.Get(conn, Meta.OpenShiftId, tx) is not null)
            throw new InvalidOperationException("Já existe um turno aberto neste caixa.");

        var shiftId = Guid.CreateVersion7();
        outbox.Append(conn, tx, EventTypes.ShiftOpened, shiftId, new ShiftOpened(shiftId, operatorName, openingCashCents));
        Meta.Set(conn, Meta.OpenShiftId, shiftId.ToString(), tx);
        Meta.Set(conn, Meta.OpenShiftOperator, operatorName, tx);

        tx.Commit();
        signal.Notify();
        return shiftId;
    }

    /// <summary>Sangria.</summary>
    public void Withdraw(long amountCents, string reason)
    {
        if (amountCents <= 0) throw new ArgumentOutOfRangeException(nameof(amountCents));

        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        var shiftId = RequireOpenShift(conn, tx);
        outbox.Append(conn, tx, EventTypes.CashWithdrawal, shiftId, new CashWithdrawal(shiftId, amountCents, reason));

        tx.Commit();
        signal.Notify();
    }

    public void Close(long declaredCashCents)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        var shiftId = RequireOpenShift(conn, tx);
        outbox.Append(conn, tx, EventTypes.ShiftClosed, shiftId, new ShiftClosed(shiftId, declaredCashCents));
        Meta.Delete(conn, Meta.OpenShiftId, tx);
        Meta.Delete(conn, Meta.OpenShiftOperator, tx);

        tx.Commit();
        signal.Notify();
    }

    internal static Guid RequireOpenShift(System.Data.IDbConnection conn, System.Data.IDbTransaction tx) =>
        Guid.TryParse(Meta.Get(conn, Meta.OpenShiftId, tx), out var id)
            ? id
            : throw new InvalidOperationException("Nenhum turno aberto neste caixa.");
}
