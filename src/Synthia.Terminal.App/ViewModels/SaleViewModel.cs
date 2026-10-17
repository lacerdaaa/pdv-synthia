using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Synthia.Contracts.Events;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Sales;

using Core = Synthia.Terminal.Core;

namespace Synthia.Terminal.App.ViewModels;

public enum PromptMode { Item, Payment, Withdrawal, CloseShift }

/// <summary>Tela de venda. Toda a regra está na <see cref="CheckoutSession"/>; aqui só teclado e exibição.</summary>
public sealed partial class SaleViewModel : ObservableObject
{
    private readonly CheckoutSession _session;
    private readonly ShiftService _shifts;
    private readonly Action _onShiftClosed;
    private bool _confirmAbandon;

    public SaleViewModel(CheckoutSession session, ShiftService shifts, Action onShiftClosed)
    {
        _session = session;
        _shifts = shifts;
        _onShiftClosed = onShiftClosed;
        Refresh();
    }

    public ObservableCollection<CartLineViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial CartLineViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string Input { get; set; } = "";

    [ObservableProperty]
    public partial PromptMode Mode { get; set; } = PromptMode.Item;

    [ObservableProperty]
    public partial string Message { get; set; } = "Leia o código de barras ou digite quantidade*código.";

    [ObservableProperty]
    public partial FeedbackKind MessageKind { get; set; } = FeedbackKind.Info;

    [ObservableProperty]
    public partial string Total { get; set; } = Formatting.Money(0);

    [ObservableProperty]
    public partial string Paid { get; set; } = Formatting.Money(0);

    [ObservableProperty]
    public partial string Remaining { get; set; } = Formatting.Money(0);

    [ObservableProperty]
    public partial string LastItem { get; set; } = "";

    [ObservableProperty]
    public partial string ItemCount { get; set; } = "0 itens";

    public bool IsPaying => _session.Stage == CheckoutStage.Paying;

    public string Prompt => Mode switch
    {
        PromptMode.Payment => "Valor recebido · F5 dinheiro · F6 débito · F7 crédito · F8 PIX · Enter = dinheiro",
        PromptMode.Withdrawal => "Valor da sangria · Enter confirma · Esc cancela",
        PromptMode.CloseShift => "Dinheiro contado na gaveta · Enter fecha o turno · Esc cancela",
        _ => "Código, PLU ou quantidade*código",
    };

    partial void OnModeChanged(PromptMode value) => OnPropertyChanged(nameof(Prompt));

    /// <summary>Atalhos de PDV. Devolve verdadeiro quando a tecla foi tratada.</summary>
    public async Task<bool> HandleKeyAsync(Key key)
    {
        if (key != Key.Escape) _confirmAbandon = false;

        switch (key)
        {
            case Key.Enter: await SubmitAsync(); return true;
            case Key.F4 when Mode == PromptMode.Item: Show(_session.StartPayment()); return true;
            case Key.F5 when Mode == PromptMode.Payment: await PayAsync(PaymentMethods.Cash); return true;
            case Key.F6 when Mode == PromptMode.Payment: await PayAsync(PaymentMethods.Debit); return true;
            case Key.F7 when Mode == PromptMode.Payment: await PayAsync(PaymentMethods.Credit); return true;
            case Key.F8 when Mode == PromptMode.Payment: await PayAsync(PaymentMethods.Pix); return true;
            case Key.Delete when Mode == PromptMode.Item:
                Show(SelectedItem is { } line ? _session.RemoveItem(line.Item.ItemId) : _session.RemoveLastItem());
                return true;
            case Key.F9 when Mode == PromptMode.Item && _session.Cart.Items.Count == 0:
                Mode = PromptMode.Withdrawal; Input = ""; return true;
            case Key.F10: Show(await _session.ReprintLastAsync()); return true;
            case Key.F12 when Mode == PromptMode.Item && _session.Cart.Items.Count == 0:
                Mode = PromptMode.CloseShift; Input = ""; return true;
            case Key.Escape: await EscapeAsync(); return true;
            default: return false;
        }
    }

    private async Task SubmitAsync()
    {
        var text = Input;
        Input = "";
        switch (Mode)
        {
            case PromptMode.Item:
                Show(await _session.EnterAsync(text));
                break;
            case PromptMode.Payment:
                await PayAsync(PaymentMethods.Cash, text);
                break;
            case PromptMode.Withdrawal:
                if (!Formatting.TryParseMoney(text, out var amount) || amount <= 0) { Show(Core.Sales.Feedback.Fail("Valor inválido.")); return; }
                Mode = PromptMode.Item;
                Show(await _session.WithdrawAsync(amount, "Sangria"));
                break;
            case PromptMode.CloseShift:
                if (!Formatting.TryParseMoney(text, out var declared)) { Show(Core.Sales.Feedback.Fail("Valor inválido.")); return; }
                _shifts.Close(declared);
                Mode = PromptMode.Item;
                _onShiftClosed();
                break;
        }
    }

    private async Task PayAsync(string method, string? typed = null)
    {
        var text = typed ?? Input;
        Input = "";
        long amount;
        if (string.IsNullOrWhiteSpace(text)) amount = _session.RemainingCents;
        else if (!Formatting.TryParseMoney(text, out amount)) { Show(Core.Sales.Feedback.Fail("Valor inválido.")); return; }

        Show(await _session.PayAsync(method, amount));
    }

    private async Task EscapeAsync()
    {
        Input = "";
        if (Mode is PromptMode.Withdrawal or PromptMode.CloseShift)
        {
            Mode = PromptMode.Item;
            Show(new Core.Sales.Feedback(FeedbackKind.Info, "Cancelado."));
        }
        else if (_session.Stage == CheckoutStage.Paying)
        {
            Show(await _session.CancelPaymentsAsync());
        }
        else if (_session.Cart.Items.Count > 0 && !_confirmAbandon)
        {
            _confirmAbandon = true;
            Show(Core.Sales.Feedback.Warn("Esc de novo descarta a venda inteira."));
        }
        else if (_session.Cart.Items.Count > 0)
        {
            _confirmAbandon = false;
            Show(await _session.AbandonSaleAsync());
        }
    }

    private void Show(Core.Sales.Feedback feedback)
    {
        Message = feedback.Message;
        MessageKind = feedback.Kind;
        Refresh();
    }

    private void Refresh()
    {
        if (Mode is PromptMode.Item or PromptMode.Payment)
            Mode = _session.Stage == CheckoutStage.Paying ? PromptMode.Payment : PromptMode.Item;

        Items.Clear();
        var n = 0;
        foreach (var item in _session.Cart.Items) Items.Add(new CartLineViewModel(++n, item));
        SelectedItem = Items.LastOrDefault();

        Total = Formatting.Money(_session.TotalCents);
        Paid = Formatting.Money(_session.PaidCents);
        Remaining = Formatting.Money(_session.RemainingCents);
        ItemCount = Items.Count == 1 ? "1 item" : $"{Items.Count} itens";
        LastItem = Items.LastOrDefault() is { } last ? $"{last.Description}  {last.Quantity} x {last.UnitPrice}" : "";
        OnPropertyChanged(nameof(IsPaying));
    }
}
