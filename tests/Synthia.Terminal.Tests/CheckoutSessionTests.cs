using Synthia.Contracts.Events;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Catalog;
using Synthia.Terminal.Core.Devices;
using Synthia.Terminal.Core.Fiscal;
using Synthia.Terminal.Core.Sales;
using Synthia.Terminal.Devices.Simulated;

namespace Synthia.Terminal.Tests;

public sealed class CheckoutSessionTests : IDisposable
{
    private readonly TestTerminal _t = new();
    private readonly SimulatedPrinter _printer = new();
    private readonly SimulatedPaymentTerminal _tef = new();
    private readonly SimulatedScale _scale = new();

    public CheckoutSessionTests()
    {
        _t.SeedProduct("Arroz 5kg", 2899, barcode: "7891000100103");
        _t.SeedProduct("Banana prata", 599, plu: 1001, version: 2);
    }

    public void Dispose() => _t.Dispose();

    private CheckoutSession Session(bool withScale = true)
    {
        var receipts = new ReceiptBuilder(new StoreSection(), "CX01", NfceEnvironment.Homologacao);
        return new CheckoutSession(_t.Catalog, new ScanResolver(_t.Catalog, ScaleBarcodeFormat.Default), _t.Sales, _t.Shifts,
            _printer, _tef, receipts, _t.Clock, withScale ? _scale : null);
    }

    [Fact]
    public async Task Sem_turno_nao_vende()
    {
        var s = Session();

        var f = await s.EnterAsync("7891000100103");

        Assert.Equal(FeedbackKind.Error, f.Kind);
        Assert.Empty(s.Cart.Items);
    }

    [Fact]
    public async Task Multiplicador_antes_do_codigo()
    {
        _t.Shifts.Open("Maria", 0);
        var s = Session();

        await s.EnterAsync("3*7891000100103");

        Assert.Equal(3, s.Cart.Items.Single().Quantity);
        Assert.Equal(8697, s.TotalCents);
    }

    [Fact]
    public async Task Produto_por_unidade_nao_aceita_quantidade_fracionada()
    {
        _t.Shifts.Open("Maria", 0);

        var f = await Session().EnterAsync("1,5*7891000100103");

        Assert.Equal(FeedbackKind.Error, f.Kind);
    }

    [Fact]
    public async Task Plu_de_produto_pesado_le_a_balanca()
    {
        _t.Shifts.Open("Maria", 0);
        _scale.Next = new ScaleReading(ScaleStatus.Stable, 1.235m);
        var s = Session();

        await s.EnterAsync("1001");

        Assert.Equal(1.235m, s.Cart.Items.Single().Quantity);
        Assert.Equal(740, s.TotalCents);
    }

    [Fact]
    public async Task Peso_instavel_nao_entra_no_carrinho()
    {
        _t.Shifts.Open("Maria", 0);
        _scale.Next = new ScaleReading(ScaleStatus.Unstable, 0);
        var s = Session();

        var f = await s.EnterAsync("1001");

        Assert.Equal(FeedbackKind.Warning, f.Kind);
        Assert.Empty(s.Cart.Items);
    }

    [Fact]
    public async Task Sem_balanca_o_peso_e_digitado()
    {
        _t.Shifts.Open("Maria", 0);
        var s = Session(withScale: false);

        Assert.Equal(FeedbackKind.Error, (await s.EnterAsync("1001")).Kind);
        await s.EnterAsync("0,5*1001");

        Assert.Equal(0.5m, s.Cart.Items.Single().Quantity);
    }

    [Fact]
    public async Task Etiqueta_de_balanca_ignora_a_balanca_do_caixa()
    {
        _t.Shifts.Open("Maria", 0);
        _scale.Next = new ScaleReading(ScaleStatus.Stable, 9m);
        var s = Session();

        await s.EnterAsync("2001001007409");

        Assert.Equal(740, s.TotalCents);
    }

    [Fact]
    public async Task Dinheiro_com_troco_finaliza_imprime_e_abre_gaveta()
    {
        _t.Shifts.Open("Maria", 0);
        var s = Session();
        await s.EnterAsync("2*7891000100103");
        s.StartPayment();

        var f = await s.PayAsync(PaymentMethods.Cash, 6000);

        Assert.Equal(FeedbackKind.Success, f.Kind);
        Assert.Contains("TROCO R$ 2,02", f.Message);
        Assert.Equal(202, s.LastSale!.ChangeCents);
        Assert.Empty(s.Cart.Items);
        Assert.Equal(CheckoutStage.Selling, s.Stage);
        Assert.Equal(1, _printer.DrawerOpenings);
        Assert.Contains("HOMOLOGAÇÃO", _printer.Printed.Single());
        Assert.Equal(2, _t.Pending()); // ShiftOpened + SaleCompleted
    }

    [Fact]
    public async Task Pagamento_misto_cartao_e_dinheiro()
    {
        _t.Shifts.Open("Maria", 0);
        var s = Session();
        await s.EnterAsync("7891000100103");
        s.StartPayment();

        var partial = await s.PayAsync(PaymentMethods.Debit, 2000);
        Assert.Equal(899, s.RemainingCents);
        await s.PayAsync(PaymentMethods.Cash, 1000);

        Assert.Equal(FeedbackKind.Info, partial.Kind);
        var sale = s.LastSale!;
        Assert.Equal(101, sale.ChangeCents);
        Assert.NotNull(sale.Payments.Single(p => p.Method == PaymentMethods.Debit).TefNsu);
        Assert.Equal(2, _printer.Printed.Count); // DANFE + via do cliente do TEF
    }

    [Fact]
    public async Task Cartao_negado_mantem_a_venda_em_pagamento()
    {
        _t.Shifts.Open("Maria", 0);
        _t.SeedProduct("Item 13", 1013, barcode: "13", version: 3);
        var s = Session();
        await s.EnterAsync("13");
        s.StartPayment();

        var f = await s.PayAsync(PaymentMethods.Credit, 1013);

        Assert.Equal(FeedbackKind.Error, f.Kind);
        Assert.Equal(CheckoutStage.Paying, s.Stage);
        Assert.Null(s.LastSale);
    }

    [Fact]
    public async Task Cartao_nao_passa_do_restante()
    {
        _t.Shifts.Open("Maria", 0);
        var s = Session();
        await s.EnterAsync("7891000100103");
        s.StartPayment();

        Assert.Equal(FeedbackKind.Error, (await s.PayAsync(PaymentMethods.Debit, 5000)).Kind);
    }

    [Fact]
    public async Task Esc_no_pagamento_volta_para_itens()
    {
        _t.Shifts.Open("Maria", 0);
        var s = Session();
        await s.EnterAsync("7891000100103");
        s.StartPayment();
        await s.PayAsync(PaymentMethods.Debit, 1000);

        await s.CancelPaymentsAsync();

        Assert.Equal(CheckoutStage.Selling, s.Stage);
        Assert.Empty(s.Payments);
        Assert.Single(s.Cart.Items);
    }

    [Fact]
    public async Task Remover_item_antes_de_finalizar_nao_gera_evento()
    {
        _t.Shifts.Open("Maria", 0);
        var s = Session();
        await s.EnterAsync("7891000100103");
        await s.EnterAsync("7891000100103");

        s.RemoveLastItem();

        Assert.Single(s.Cart.Items);
        Assert.Equal(1, _t.Pending()); // só ShiftOpened
    }

    [Fact]
    public async Task Impressora_fora_nao_impede_a_venda()
    {
        _t.Shifts.Open("Maria", 0);
        var s = new CheckoutSession(_t.Catalog, new ScanResolver(_t.Catalog, ScaleBarcodeFormat.Default), _t.Sales, _t.Shifts,
            new BrokenPrinter(), _tef, new ReceiptBuilder(new StoreSection(), "CX01", NfceEnvironment.Producao), _t.Clock);
        await s.EnterAsync("7891000100103");
        s.StartPayment();

        var f = await s.PayAsync(PaymentMethods.Cash, 2899);

        Assert.Equal(FeedbackKind.Warning, f.Kind);
        Assert.NotNull(s.LastSale);
    }

    [Fact]
    public async Task Sangria_imprime_comprovante_e_abre_gaveta()
    {
        _t.Shifts.Open("Maria", 10000);
        var s = Session();

        var f = await s.WithdrawAsync(5000, "cofre");

        Assert.Equal(FeedbackKind.Success, f.Kind);
        Assert.Contains("SANGRIA", _printer.Printed.Single());
        Assert.Equal(1, _printer.DrawerOpenings);
    }

    [Fact]
    public void Chave_da_nfce_tem_44_digitos_e_dv_valido()
    {
        var key = NfceKey.Create(_t.Clock.GetUtcNow(), "00.000.000/0001-91", 2, 123, offline: true, 12345678);

        Assert.True(NfceKey.IsValid(key.Value));
        Assert.Equal(2, key.Serie);
        Assert.Equal(123, key.Number);
        Assert.Equal(9, key.EmissionType);
    }

    [Fact]
    public async Task Numeracao_da_nfce_e_sequencial_e_persistente()
    {
        var numbering = new NfceNumbering(_t.Db, 1);
        var emitter = new SimulatedNfceEmitter(numbering, "00000000000191", _t.Clock);
        var sale = new SaleCompleted(Guid.NewGuid(), Guid.NewGuid(), [], [], 0, 0, null, null);

        var first = new NfceKey((await emitter.EmitAsync(sale)).Key!);
        var second = new NfceKey((await new SimulatedNfceEmitter(new NfceNumbering(_t.Db, 1), "00000000000191", _t.Clock).EmitAsync(sale)).Key!);

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
    }

    private sealed class BrokenPrinter : IReceiptPrinter
    {
        public Task PrintAsync(ReceiptDocument document, CancellationToken ct = default) => throw new IOException("sem papel");
        public Task OpenDrawerAsync(CancellationToken ct = default) => throw new IOException("sem papel");
    }
}
