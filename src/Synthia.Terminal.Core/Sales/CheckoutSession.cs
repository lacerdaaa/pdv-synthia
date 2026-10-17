using Synthia.Contracts.Events;
using Synthia.Terminal.Core.Catalog;
using Synthia.Terminal.Core.Devices;

namespace Synthia.Terminal.Core.Sales;

public enum FeedbackKind { Info, Success, Warning, Error }

public sealed record Feedback(FeedbackKind Kind, string Message)
{
    public static Feedback Ok(string m) => new(FeedbackKind.Success, m);
    public static Feedback Warn(string m) => new(FeedbackKind.Warning, m);
    public static Feedback Fail(string m) => new(FeedbackKind.Error, m);
}

public enum CheckoutStage { Selling, Paying }

/// <summary>
/// A venda em andamento no caixa, independente de tela: entrada de códigos, balança, pagamentos com TEF,
/// finalização, impressão e gaveta. A interface só repassa o que o operador digita e mostra o estado.
/// </summary>
public sealed class CheckoutSession(
    LocalCatalog catalog,
    ScanResolver resolver,
    SaleService sales,
    ShiftService shifts,
    IReceiptPrinter printer,
    IPaymentTerminal tef,
    ReceiptBuilder receipts,
    TimeProvider clock,
    IScale? scale = null)
{
    private readonly List<SalePayment> _payments = [];
    private readonly List<PaymentResult> _tefApprovals = [];
    private Guid _saleId = Guid.CreateVersion7();

    public Cart Cart { get; private set; } = new();
    public CheckoutStage Stage { get; private set; } = CheckoutStage.Selling;
    public IReadOnlyList<SalePayment> Payments => _payments;
    public SaleCompleted? LastSale { get; private set; }
    public Feedback? LastFeedback { get; private set; }
    public bool HasScale => scale is not null;

    public long TotalCents => Cart.TotalCents;
    public long PaidCents => _payments.Sum(p => p.AmountCents);
    public long RemainingCents => Math.Max(0, TotalCents - PaidCents);

    public event EventHandler? Changed;

    /// <summary>
    /// Entrada do operador: código de barras, etiqueta de balança, PLU, ou multiplicador antes do código
    /// (<c>3*7891000100103</c>, <c>1,235*1001</c>).
    /// </summary>
    public async Task<Feedback> EnterAsync(string input, CancellationToken ct = default)
    {
        input = input.Trim();
        if (input.Length == 0) return Report(Feedback.Warn("Leia ou digite um código."));
        if (Stage == CheckoutStage.Paying) return Report(Feedback.Fail("Venda em pagamento. Esc volta para os itens."));
        if (shifts.CurrentShiftId() is null) return Report(Feedback.Fail("Abra o turno antes de vender."));

        decimal? multiplier = null;
        var code = input;
        var star = input.IndexOf('*');
        if (star >= 0)
        {
            if (!Formatting.TryParseQuantity(input[..star], out var m))
                return Report(Feedback.Fail($"Quantidade inválida: {input[..star]}"));
            multiplier = m;
            code = input[(star + 1)..].Trim();
        }

        var scan = resolver.Resolve(code);
        if (scan is null && code.Length <= 6 && int.TryParse(code, out var plu) && catalog.FindByPlu(plu) is { } byPlu)
            scan = new ScanResult(byPlu, 1, null);
        if (scan is null) return Report(Feedback.Fail($"Produto não encontrado: {code}"));

        var product = scan.Product;
        decimal quantity;
        if (scan.FromScaleLabel)
        {
            quantity = scan.Quantity;
        }
        else if (IsWeighed(product) && multiplier is null)
        {
            var weighed = await WeighAsync(product, ct);
            if (weighed.Feedback is { } problem) return Report(problem);
            quantity = weighed.Kg;
        }
        else
        {
            if (!IsWeighed(product) && multiplier is { } m && m != decimal.Truncate(m))
                return Report(Feedback.Fail($"{product.Name} é vendido por unidade."));
            quantity = multiplier ?? 1;
        }

        var item = Cart.Add(product, quantity, scan.FixedTotalCents);
        return Report(Feedback.Ok($"{item.Description} · {Formatting.Quantity(item.Quantity)} x {Formatting.Money(item.UnitPriceCents)} = {Formatting.Money(item.TotalCents)}"));
    }

    /// <summary>Remove um item antes de finalizar: nada foi vendido ainda, então não gera evento.</summary>
    public Feedback RemoveItem(Guid itemId)
    {
        if (Stage == CheckoutStage.Paying) return Report(Feedback.Fail("Volte para os itens (Esc) antes de remover."));
        var item = Cart.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (item is null || !Cart.Remove(itemId)) return Report(Feedback.Fail("Item não encontrado."));
        return Report(Feedback.Warn($"Removido: {item.Description} ({Formatting.Money(item.TotalCents)})"));
    }

    public Feedback RemoveLastItem() =>
        Cart.Items.Count == 0 ? Report(Feedback.Warn("Nenhum item para remover.")) : RemoveItem(Cart.Items[^1].ItemId);

    public Feedback StartPayment()
    {
        if (Cart.Items.Count == 0) return Report(Feedback.Warn("Nenhum item na venda."));
        Stage = CheckoutStage.Paying;
        return Report(new Feedback(FeedbackKind.Info, $"Total {Formatting.Money(TotalCents)}. Escolha a forma de pagamento."));
    }

    /// <summary>Registra um pagamento; cartão e PIX passam pelo TEF. Quitou, finaliza a venda.</summary>
    public async Task<Feedback> PayAsync(string method, long amountCents, CancellationToken ct = default)
    {
        if (Stage != CheckoutStage.Paying) return Report(Feedback.Fail("Inicie o pagamento primeiro."));
        if (amountCents <= 0) return Report(Feedback.Fail("Valor inválido."));
        if (method != PaymentMethods.Cash && amountCents > RemainingCents)
            return Report(Feedback.Fail("Cartão e PIX não podem passar do valor restante."));

        if (method == PaymentMethods.Cash)
        {
            _payments.Add(new SalePayment(method, amountCents));
        }
        else
        {
            var result = await tef.AuthorizeAsync(new PaymentRequest(_saleId, method, amountCents), ct);
            if (!result.Approved) return Report(Feedback.Fail($"{ReceiptBuilder.MethodName(method)} negado: {result.Message}"));
            _tefApprovals.Add(result);
            _payments.Add(new SalePayment(method, amountCents, result.Nsu));
        }

        if (PaidCents >= TotalCents) return await FinishAsync(ct);
        return Report(new Feedback(FeedbackKind.Info, $"Recebido {Formatting.Money(amountCents)}. Falta {Formatting.Money(RemainingCents)}."));
    }

    /// <summary>Esc no pagamento: estorna o que o TEF já aprovou e volta para os itens.</summary>
    public async Task<Feedback> CancelPaymentsAsync(CancellationToken ct = default)
    {
        await ReverseTefAsync(ct);
        _payments.Clear();
        Stage = CheckoutStage.Selling;
        return Report(new Feedback(FeedbackKind.Info, "Pagamentos desfeitos. Venda aberta para itens."));
    }

    /// <summary>Descarta a venda em andamento (antes de finalizada).</summary>
    public async Task<Feedback> AbandonSaleAsync(CancellationToken ct = default)
    {
        if (Cart.Items.Count == 0 && _payments.Count == 0) return Report(Feedback.Warn("Nenhuma venda em andamento."));
        await ReverseTefAsync(ct);
        Reset();
        return Report(Feedback.Warn("Venda descartada."));
    }

    public async Task<Feedback> WithdrawAsync(long amountCents, string reason, CancellationToken ct = default)
    {
        if (Cart.Items.Count > 0) return Report(Feedback.Fail("Finalize ou descarte a venda antes da sangria."));
        try { shifts.Withdraw(amountCents, reason); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException) { return Report(Feedback.Fail(ex.Message)); }

        var printed = await TryPrintAsync(receipts.CashWithdrawal(amountCents, reason, shifts.CurrentOperator() ?? "", clock.GetUtcNow()), ct);
        await TryOpenDrawerAsync(ct);
        return Report(printed ? Feedback.Ok($"Sangria de {Formatting.Money(amountCents)} registrada.")
                              : Feedback.Warn($"Sangria registrada; impressora não respondeu."));
    }

    public async Task<Feedback> ReprintLastAsync(CancellationToken ct = default)
    {
        if (LastSale is null) return Report(Feedback.Warn("Nenhuma venda para reimprimir."));
        var ok = await TryPrintAsync(receipts.Sale(LastSale, shifts.CurrentOperator() ?? "", clock.GetUtcNow()), ct);
        return Report(ok ? Feedback.Ok("Cupom reimpresso.") : Feedback.Fail("Impressora não respondeu."));
    }

    private async Task<Feedback> FinishAsync(CancellationToken ct)
    {
        var sale = await sales.CompleteAsync(Cart, [.. _payments], _saleId, ct);
        LastSale = sale;

        var printed = await TryPrintAsync(receipts.Sale(sale, shifts.CurrentOperator() ?? "", clock.GetUtcNow()), ct);
        foreach (var approval in _tefApprovals)
            printed &= await TryPrintAsync(receipts.PaymentSlip(approval, "VIA DO CLIENTE"), ct);
        if (sale.Payments.Any(p => p.Method == PaymentMethods.Cash))
            await TryOpenDrawerAsync(ct);

        Reset();
        var change = sale.ChangeCents > 0 ? $" · TROCO {Formatting.Money(sale.ChangeCents)}" : "";
        var contingency = sale.NfceStatus == NfceStatus.Pending ? " · NFC-e em contingência" : "";
        return Report(printed
            ? Feedback.Ok($"Venda finalizada{change}{contingency}")
            : Feedback.Warn($"Venda finalizada{change}{contingency} · impressora não respondeu, reimprima (F10)"));
    }

    private async Task<(decimal Kg, Feedback? Feedback)> WeighAsync(CatalogItem product, CancellationToken ct)
    {
        if (scale is null)
            return (0, Feedback.Fail($"{product.Name} é pesado: digite peso*código (ex.: 1,235*{product.Plu ?? 0})."));

        var reading = await scale.ReadAsync(ct);
        return reading.Status switch
        {
            ScaleStatus.Stable when reading.Kg > 0 => (reading.Kg, null),
            ScaleStatus.Stable => (0, Feedback.Warn($"Coloque {product.Name} na balança.")),
            ScaleStatus.Unstable => (0, Feedback.Warn("Peso instável. Aguarde e leia de novo.")),
            ScaleStatus.Overload => (0, Feedback.Fail("Balança em sobrecarga.")),
            ScaleStatus.Negative => (0, Feedback.Fail("Peso negativo: zere a balança (tara).")),
            _ => (0, Feedback.Fail($"Balança não respondeu: digite peso*código (ex.: 1,235*{product.Plu ?? 0}).")),
        };
    }

    private static bool IsWeighed(CatalogItem product) => product.Unit.Equals("KG", StringComparison.OrdinalIgnoreCase);

    private async Task ReverseTefAsync(CancellationToken ct)
    {
        foreach (var approval in _tefApprovals.Where(a => a.Nsu is not null))
        {
            var amount = _payments.First(p => p.TefNsu == approval.Nsu).AmountCents;
            await tef.ReverseAsync(approval.Nsu!, amount, ct);
        }
        _tefApprovals.Clear();
    }

    private async Task<bool> TryPrintAsync(ReceiptDocument doc, CancellationToken ct)
    {
        try { await printer.PrintAsync(doc, ct); return true; }
        catch (Exception ex) when (ex is not OperationCanceledException) { return false; }
    }

    private async Task TryOpenDrawerAsync(CancellationToken ct)
    {
        try { await printer.OpenDrawerAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* gaveta abre na chave */ }
    }

    private void Reset()
    {
        Cart = new Cart();
        _payments.Clear();
        _tefApprovals.Clear();
        _saleId = Guid.CreateVersion7();
        Stage = CheckoutStage.Selling;
    }

    private Feedback Report(Feedback feedback)
    {
        LastFeedback = feedback;
        Changed?.Invoke(this, EventArgs.Empty);
        return feedback;
    }
}
