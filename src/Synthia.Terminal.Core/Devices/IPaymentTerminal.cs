namespace Synthia.Terminal.Core.Devices;

/// <param name="Method">debit, credit ou pix (ver <c>PaymentMethods</c>).</param>
public sealed record PaymentRequest(Guid SaleId, string Method, long AmountCents, int Installments = 1);

/// <param name="Nsu">Número do comprovante no TEF; vai para <c>SalePayment.TefNsu</c>.</param>
public sealed record PaymentResult(
    bool Approved,
    string? Nsu,
    string? AuthorizationCode,
    string? Acquirer,
    string Message,
    IReadOnlyList<string> CustomerReceipt,
    IReadOnlyList<string> MerchantReceipt);

/// <summary>
/// TEF: o caixa não fala com o pinpad; fala com o cliente TEF (SiTef, PayGo...), que conduz o pinpad,
/// a senha e a autorização com a adquirente.
/// </summary>
public interface IPaymentTerminal
{
    Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default);

    /// <summary>Estorno de uma transação aprovada (venda cancelada).</summary>
    Task<PaymentResult> ReverseAsync(string nsu, long amountCents, CancellationToken ct = default);
}
