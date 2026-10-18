using Synthia.Contracts.Events;
using Synthia.Terminal.Core.Devices;
using Synthia.Terminal.Core.Fiscal;
using static Synthia.Terminal.Core.Formatting;

namespace Synthia.Terminal.Core.Sales;

/// <summary>
/// Layout dos documentos impressos no caixa. O DANFE aqui segue a estrutura do leiaute da NFC-e
/// (cabeçalho, itens, totais, pagamentos, consulta, consumidor, identificação, QR Code); o conteúdo
/// fiscal definitivo (QR Code com CSC, protocolo) virá da ACBr.
/// </summary>
public sealed class ReceiptBuilder(Func<StoreSection> store, string terminalId, NfceEnvironment environment)
{
    public ReceiptBuilder(StoreSection store, string terminalId, NfceEnvironment environment)
        : this(() => store, terminalId, environment) { }

    private static readonly Dictionary<string, string> MethodNames = new()
    {
        [PaymentMethods.Cash] = "Dinheiro",
        [PaymentMethods.Debit] = "Cartão de Débito",
        [PaymentMethods.Credit] = "Cartão de Crédito",
        [PaymentMethods.Pix] = "PIX",
    };

    public static string MethodName(string method) => MethodNames.GetValueOrDefault(method, method);

    public ReceiptDocument Sale(SaleCompleted sale, string operatorName, DateTimeOffset issuedAt)
    {
        var lines = Header();
        lines.Add(new ReceiptLine.Text("DANFE NFC-e - Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica", Align.Center));
        AddEnvironmentWarning(lines);
        lines.Add(new ReceiptLine.Separator());

        for (var i = 0; i < sale.Items.Count; i++)
        {
            var item = sale.Items[i];
            lines.Add(new ReceiptLine.Text($"{i + 1:000} {item.Description}"));
            lines.Add(new ReceiptLine.Columns($"    {Quantity(item.Quantity)} x {Amount(item.UnitPriceCents)}", Amount(item.TotalCents)));
        }

        lines.Add(new ReceiptLine.Separator());
        lines.Add(new ReceiptLine.Columns("Qtde. total de itens", sale.Items.Count.ToString()));
        lines.Add(new ReceiptLine.Columns("Valor a pagar R$", Amount(sale.TotalCents)));
        lines.Add(new ReceiptLine.Columns("FORMA DE PAGAMENTO", "VALOR PAGO R$"));
        foreach (var p in sale.Payments)
            lines.Add(new ReceiptLine.Columns(MethodName(p.Method), Amount(p.AmountCents)));
        if (sale.ChangeCents > 0)
            lines.Add(new ReceiptLine.Columns("Troco R$", Amount(sale.ChangeCents)));
        lines.Add(new ReceiptLine.Separator());

        if (sale.NfceKey is { } rawKey && !NfceKey.IsValid(rawKey))
        {
            lines.Add(new ReceiptLine.Text($"Chave: {rawKey}", Align.Center));
        }
        else if (sale.NfceKey is { } validKey)
        {
            var key = new NfceKey(validKey);
            if (sale.NfceStatus == NfceStatus.Pending)
            {
                lines.Add(new ReceiptLine.Text("EMITIDA EM CONTINGÊNCIA", Align.Center, Bold: true));
                lines.Add(new ReceiptLine.Text("Pendente de autorização", Align.Center));
            }
            lines.Add(new ReceiptLine.Text("Consulte pela Chave de Acesso em", Align.Center));
            lines.Add(new ReceiptLine.Text("www.nfce.fazenda.sp.gov.br/consulta", Align.Center));
            lines.Add(new ReceiptLine.Text(key.Formatted, Align.Center));
            lines.Add(new ReceiptLine.Text("CONSUMIDOR NÃO IDENTIFICADO", Align.Center, Bold: true));
            lines.Add(new ReceiptLine.Text(
                $"NFC-e nº {key.Number:000000000} Série {key.Serie:000} {issuedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}", Align.Center, Bold: true));
            lines.Add(new ReceiptLine.QrCode($"https://www.nfce.fazenda.sp.gov.br/qrcode?p={key.Value}"));
        }

        if (store().ReceiptFooter is { Length: > 0 } footer) lines.Add(new ReceiptLine.Text(footer, Align.Center));
        lines.Add(new ReceiptLine.Text($"{terminalId} · Operador: {operatorName}", Align.Center));
        return new ReceiptDocument(lines);
    }

    public ReceiptDocument PaymentSlip(PaymentResult result, string copy)
    {
        var lines = new List<ReceiptLine> { new ReceiptLine.Text(copy, Align.Center, Bold: true) };
        lines.AddRange(result.CustomerReceipt.Select(l => (ReceiptLine)new ReceiptLine.Text(l)));
        return new ReceiptDocument(lines);
    }

    public ReceiptDocument CashWithdrawal(long amountCents, string reason, string operatorName, DateTimeOffset at)
    {
        var lines = Header();
        lines.Add(new ReceiptLine.Text("SANGRIA", Align.Center, Bold: true));
        lines.Add(new ReceiptLine.Columns("Valor", Money(amountCents)));
        lines.Add(new ReceiptLine.Columns("Motivo", reason));
        lines.Add(new ReceiptLine.Columns("Data", at.ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr)));
        lines.Add(new ReceiptLine.Feed(2));
        lines.Add(new ReceiptLine.Text("______________________________", Align.Center));
        lines.Add(new ReceiptLine.Text(operatorName, Align.Center));
        return new ReceiptDocument(lines);
    }

    private List<ReceiptLine> Header()
    {
        var s = store();
        var lines = new List<ReceiptLine>
        {
            new ReceiptLine.Text(s.Name, Align.Center, Bold: true),
            new ReceiptLine.Text($"CNPJ {Synthia.Contracts.Cnpj.Format(s.Cnpj)}", Align.Center),
        };
        if (!string.IsNullOrWhiteSpace(s.StateRegistration)) lines.Add(new ReceiptLine.Text($"IE {s.StateRegistration}", Align.Center));
        if (!string.IsNullOrWhiteSpace(s.Address)) lines.Add(new ReceiptLine.Text(s.Address, Align.Center));
        lines.Add(new ReceiptLine.Separator());
        return lines;
    }

    private void AddEnvironmentWarning(List<ReceiptLine> lines)
    {
        if (environment == NfceEnvironment.Homologacao)
            lines.Add(new ReceiptLine.Text("EMITIDA EM AMBIENTE DE HOMOLOGAÇÃO - SEM VALOR FISCAL", Align.Center, Bold: true));
    }
}
