using Synthia.Contracts;
using Synthia.Contracts.Events;
using Synthia.Terminal.Core.Sales;

namespace Synthia.Terminal.Tests;

public sealed class SaleTests : IDisposable
{
    private readonly TestTerminal _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Venda_grava_um_evento_na_outbox_com_total_e_troco()
    {
        var arroz = _t.SeedProduct("Arroz 5kg", 2899, barcode: "789001");
        _t.Shifts.Open("Maria", 10000);

        var cart = new Cart();
        cart.Add(_t.Catalog.FindByBarcode("789001")!, 2);
        var sale = await _t.Sales.CompleteAsync(cart, [new SalePayment(PaymentMethods.Cash, 6000)]);

        Assert.Equal(5798, sale.TotalCents);
        Assert.Equal(202, sale.ChangeCents);
        Assert.Equal(NfceStatus.Authorized, sale.NfceStatus);
        Assert.Equal(arroz.Id, sale.Items.Single().ProductId);
        Assert.Equal(2, _t.Pending()); // ShiftOpened + SaleCompleted
    }

    [Fact]
    public async Task Item_de_balanca_arredonda_para_o_centavo()
    {
        _t.SeedProduct("Banana", 599, plu: 1001);
        _t.Shifts.Open("Maria", 0);

        var cart = new Cart();
        cart.Add(_t.Catalog.FindByPlu(1001)!, 1.235m);
        var sale = await _t.Sales.CompleteAsync(cart, [new SalePayment(PaymentMethods.Pix, 740)]);

        Assert.Equal(740, sale.TotalCents); // 1,235 kg × R$ 5,99 = R$ 7,39765
    }

    [Fact]
    public async Task Sem_turno_aberto_nada_e_gravado()
    {
        _t.SeedProduct("Pão", 99, barcode: "1");
        var cart = new Cart();
        cart.Add(_t.Catalog.FindByBarcode("1")!, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _t.Sales.CompleteAsync(cart, [new SalePayment(PaymentMethods.Cash, 99)]));
        Assert.Equal(0, _t.Pending());
    }

    [Fact]
    public async Task Troco_nao_pode_sair_de_cartao()
    {
        _t.SeedProduct("Pão", 99, barcode: "1");
        _t.Shifts.Open("Maria", 0);
        var cart = new Cart();
        cart.Add(_t.Catalog.FindByBarcode("1")!, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _t.Sales.CompleteAsync(cart, [new SalePayment(PaymentMethods.Debit, 500)]));
    }

    [Fact]
    public async Task Sem_internet_a_nfce_fica_pendente_e_a_venda_segue()
    {
        _t.SeedProduct("Pão", 99, barcode: "1");
        _t.Shifts.Open("Maria", 0);
        _t.Nfce.Status = NfceStatus.Pending;
        _t.Server.Online = false;

        var cart = new Cart();
        cart.Add(_t.Catalog.FindByBarcode("1")!, 1);
        var sale = await _t.Sales.CompleteAsync(cart, [new SalePayment(PaymentMethods.Cash, 99)]);

        Assert.Equal(NfceStatus.Pending, sale.NfceStatus);
        Assert.Equal(2, _t.Pending());
    }

    [Fact]
    public async Task Cancelamento_vira_novo_evento_no_mesmo_stream()
    {
        _t.SeedProduct("Pão", 99, barcode: "1");
        _t.Shifts.Open("Maria", 0);
        var cart = new Cart();
        var item = cart.Add(_t.Catalog.FindByBarcode("1")!, 1);
        var sale = await _t.Sales.CompleteAsync(cart, [new SalePayment(PaymentMethods.Cash, 99)]);

        _t.Sales.CancelItem(sale.SaleId, item.ItemId, "erro do operador");
        await _t.Pusher.PushPendingAsync();

        var cancel = _t.Server.Events.Values.Single(e => e.Type == EventTypes.SaleItemCancelled);
        Assert.Equal(sale.SaleId, cancel.StreamId);
        Assert.Equal(2, cancel.StreamSeq);
    }

    [Fact]
    public void Turno_registra_sangria_e_fechamento()
    {
        _t.Shifts.Open("Maria", 10000);
        _t.Shifts.Withdraw(5000, "sangria");
        _t.Shifts.Close(5000);

        Assert.Null(_t.Shifts.CurrentShiftId());
        Assert.Equal(3, _t.Pending());
        Assert.Throws<InvalidOperationException>(() => _t.Shifts.Withdraw(100, "sem turno"));
    }
}
