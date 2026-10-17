namespace Synthia.Contracts.Events;

// Valores monetários sempre em centavos (long) para evitar arredondamento em ponto flutuante.
// Quantidades em decimal: unidades inteiras ou kg com 3 casas (itens de balança).

public sealed record ShiftOpened(Guid ShiftId, string Operator, long OpeningCashCents);

public sealed record ShiftClosed(Guid ShiftId, long DeclaredCashCents);

public sealed record SaleItem(
    Guid ItemId,
    Guid ProductId,
    string Description,
    decimal Quantity,
    long UnitPriceCents,
    long TotalCents);

public static class PaymentMethods
{
    public const string Cash = "cash";
    public const string Debit = "debit";
    public const string Credit = "credit";
    public const string Pix = "pix";
}

public sealed record SalePayment(string Method, long AmountCents, string? TefNsu = null);

public static class NfceStatus
{
    public const string Authorized = "authorized";
    /// <summary>Emitida em contingência offline (tpEmis 9), aguardando transmissão.</summary>
    public const string Pending = "pending";
    public const string Rejected = "rejected";
}

public sealed record SaleCompleted(
    Guid SaleId,
    Guid ShiftId,
    IReadOnlyList<SaleItem> Items,
    IReadOnlyList<SalePayment> Payments,
    long TotalCents,
    long ChangeCents,
    string? NfceKey,
    string? NfceStatus);

/// <param name="RefundMethod">
/// Como o valor voltou ao cliente (um de <see cref="PaymentMethods"/>). Em dinheiro, sai da gaveta.
/// Nulo em eventos antigos: tratado como sem impacto no dinheiro.
/// </param>
public sealed record SaleItemCancelled(Guid SaleId, Guid ItemId, string Reason, string? RefundMethod = null);

public sealed record SaleCancelled(Guid SaleId, string Reason);

public sealed record CashWithdrawal(Guid ShiftId, long AmountCents, string Reason);

/// <param name="InventoryId">Inventário da retaguarda que originou a contagem (nulo quando veio de outro lugar).</param>
/// <param name="ExpectedQuantity">Saldo do sistema no momento da contagem, para relatório de divergência.</param>
public sealed record StockCounted(
    Guid ProductId,
    decimal CountedQuantity,
    Guid? InventoryId = null,
    decimal? ExpectedQuantity = null,
    string? CountedBy = null);

public static class StockReceiptSources
{
    /// <summary>Mercadoria trazida da loja antiga na mudança.</summary>
    public const string Transfer = "transfer";
    /// <summary>Compra de fornecedor (com ou sem XML da NF-e).</summary>
    public const string Supplier = "supplier";
    public const string Other = "other";
}

/// <param name="UnitCostCents">Custo unitário quando conhecido (nota do fornecedor); nulo em transferência.</param>
public sealed record StockReceiptItem(Guid ProductId, string Description, decimal Quantity, long? UnitCostCents);

/// <summary>
/// Entrada de mercadoria lançada na retaguarda. Stream = a própria entrada.
/// </summary>
/// <param name="Document">Número da nota, romaneio da transferência etc.</param>
/// <param name="ReceivedBy">Usuário da retaguarda que lançou.</param>
public sealed record StockReceived(
    Guid ReceiptId,
    string Source,
    string? Supplier,
    string? Document,
    IReadOnlyList<StockReceiptItem> Items,
    string ReceivedBy,
    string? Note);

public sealed record NfceAuthorized(Guid SaleId, string NfceKey, string Protocol);

public sealed record NfceRejected(Guid SaleId, string NfceKey, string Code, string Reason);
