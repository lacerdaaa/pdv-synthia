using Microsoft.Extensions.DependencyInjection;
using Synthia.Contracts;
using Synthia.Contracts.Events;
using Synthia.Server.Backoffice;
using Synthia.Server.Sync;
using static Synthia.Server.Tests.EventBuilder;

namespace Synthia.Server.Tests;

public sealed class ReportServiceTests : IAsyncLifetime
{
    private readonly ServerFixture fx = new();

    public Task InitializeAsync() => fx.InitializeAsync();
    public Task DisposeAsync() => fx.DisposeAsync();

    private T Get<T>() where T : notnull => fx.Factory.Services.GetRequiredService<T>();

    /// <summary>Hoje no fuso da loja, à hora pedida, em UTC.</summary>
    private DateTimeOffset At(int hour, int daysAgo = 0)
    {
        var reports = Get<ReportService>();
        var day = reports.Today(DateTimeOffset.UtcNow).AddDays(-daysAgo);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, 0)), reports.Zone), TimeSpan.Zero);
    }

    private static EventEnvelope SaleEvent(SaleCompleted sale, string terminal, DateTimeOffset at) =>
        Make(EventTypes.SaleCompleted, sale.SaleId, 1, sale, at) with { TerminalId = terminal };

    [Fact]
    public async Task Relatorio_de_vendas_desconta_cancelamentos_e_separa_por_forma_caixa_e_hora()
    {
        var arroz = Guid.CreateVersion7();
        var feijao = Guid.CreateVersion7();
        var shift = Guid.CreateVersion7();
        SaleItem Item(Guid p, string d, decimal q, long unit) => new(Guid.CreateVersion7(), p, d, q, unit, (long)(q * unit));

        // CX01, 10h: 2 arroz em dinheiro, paga 60 → troco 2,02.
        var s1 = new SaleCompleted(Guid.CreateVersion7(), shift, [Item(arroz, "Arroz", 2, 2899)], [new SalePayment(PaymentMethods.Cash, 6000)], 5798, 202, null, null);
        // CX02, 15h: arroz + feijão no débito; o feijão é devolvido em dinheiro.
        var s2Items = new[] { Item(arroz, "Arroz", 1, 2899), Item(feijao, "Feijão", 1, 899) };
        var s2 = new SaleCompleted(Guid.CreateVersion7(), shift, s2Items, [new SalePayment(PaymentMethods.Debit, 3798)], 3798, 0, null, null);
        // CX01, 16h: venda cancelada inteira.
        var s3 = new SaleCompleted(Guid.CreateVersion7(), shift, [Item(feijao, "Feijão", 5, 899)], [new SalePayment(PaymentMethods.Pix, 4495)], 4495, 0, null, null);
        // Ontem: fora do período.
        var s4 = new SaleCompleted(Guid.CreateVersion7(), shift, [Item(arroz, "Arroz", 10, 2899)], [new SalePayment(PaymentMethods.Cash, 28990)], 28990, 0, null, null);

        await Get<EventStore>().AppendAsync(
        [
            SaleEvent(s1, "CX01", At(10)),
            SaleEvent(s2, "CX02", At(15)),
            Make(EventTypes.SaleItemCancelled, s2.SaleId, 2, new SaleItemCancelled(s2.SaleId, s2Items[1].ItemId, "desistiu", PaymentMethods.Cash), At(15)),
            SaleEvent(s3, "CX01", At(16)),
            Make(EventTypes.SaleCancelled, s3.SaleId, 2, new SaleCancelled(s3.SaleId, "erro"), At(16)),
            SaleEvent(s4, "CX01", At(11, daysAgo: 1)),
        ]);

        var reports = Get<ReportService>();
        var today = reports.Today(DateTimeOffset.UtcNow);
        var r = await reports.SalesAsync(today, today);

        Assert.Equal(2, r.Sales);                       // s1 e s2; s3 cancelada, s4 é de ontem
        Assert.Equal(1, r.CancelledSales);
        Assert.Equal(5798 + 3798 + 4495, r.GrossCents);
        Assert.Equal(899 + 4495, r.CancelledCents);
        Assert.Equal(5798 + 2899, r.NetCents);
        Assert.Equal((5798 + 2899) / 2, r.AverageTicketCents);

        Assert.Equal(5798 - 899, r.Payments.Single(p => p.Method == PaymentMethods.Cash).NetCents); // 60 − troco − devolução
        Assert.Equal(3798, r.Payments.Single(p => p.Method == PaymentMethods.Debit).NetCents);
        Assert.DoesNotContain(r.Payments, p => p.Method == PaymentMethods.Pix);

        Assert.Equal(5798, r.NetByHour[10]);
        Assert.Equal(2899, r.NetByHour[15]);
        Assert.Equal(["CX01", "CX02"], r.Terminals.Select(t => t.TerminalId));
        Assert.Equal(3 * 2899, r.TopProducts[0].NetCents); // arroz: 2 + 1
        Assert.Single(r.TopProducts);                      // feijão saiu inteiro (devolvido / cancelado)

        var cx02 = await reports.SalesAsync(today, today, "CX02");
        Assert.Equal(1, cx02.Sales);

        var twoDays = await reports.SalesAsync(today.AddDays(-1), today);
        Assert.Equal(3, twoDays.Sales);
        Assert.Equal(2, twoDays.Days.Count);
    }

    [Fact]
    public async Task Conferencia_de_turno_mostra_esperado_declarado_e_diferenca()
    {
        var shift = Guid.CreateVersion7();
        var open = Guid.CreateVersion7();
        var item = new SaleItem(Guid.CreateVersion7(), Guid.CreateVersion7(), "Arroz", 1, 2899, 2899);
        var sale = new SaleCompleted(Guid.CreateVersion7(), shift, [item], [new SalePayment(PaymentMethods.Cash, 3000)], 2899, 101, null, null);

        await Get<EventStore>().AppendAsync(
        [
            Make(EventTypes.ShiftOpened, shift, 1, new ShiftOpened(shift, "Maria", 10000), At(8)),
            SaleEvent(sale, "CX01", At(9)),
            Make(EventTypes.CashWithdrawal, shift, 2, new CashWithdrawal(shift, 5000, "cofre"), At(12)),
            Make(EventTypes.ShiftClosed, shift, 3, new ShiftClosed(shift, 7889), At(18)),   // faltam 10 centavos
            Make(EventTypes.ShiftOpened, open, 1, new ShiftOpened(open, "João", 10000), At(13)),
        ]);

        var reports = Get<ReportService>();
        var today = reports.Today(DateTimeOffset.UtcNow);
        var shifts = await reports.ShiftsAsync(today, today);

        var maria = shifts.Single(s => s.ShiftId == shift);
        Assert.Equal(10000 + 2899 - 5000, maria.ExpectedCashCents);
        Assert.Equal(7889, maria.DeclaredCashCents);
        Assert.Equal(-10, maria.DifferenceCents);
        Assert.True(maria.Closed);

        var joao = shifts.Single(s => s.ShiftId == open);
        Assert.False(joao.Closed);
        Assert.Null(joao.DifferenceCents);
    }
}
