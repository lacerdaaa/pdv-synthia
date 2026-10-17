namespace Synthia.Contracts.Events;

public static class EventTypes
{
    public const string ShiftOpened = nameof(ShiftOpened);
    public const string ShiftClosed = nameof(ShiftClosed);
    public const string SaleCompleted = nameof(SaleCompleted);
    public const string SaleItemCancelled = nameof(SaleItemCancelled);
    public const string SaleCancelled = nameof(SaleCancelled);
    public const string CashWithdrawal = nameof(CashWithdrawal);
    public const string StockCounted = nameof(StockCounted);
    public const string StockReceived = nameof(StockReceived);
    public const string StockAdjusted = nameof(StockAdjusted);
    public const string NfceAuthorized = nameof(NfceAuthorized);
    public const string NfceRejected = nameof(NfceRejected);
}
