using System.Text;
using Synthia.Terminal.Core.Devices;

namespace Synthia.Terminal.Devices.Simulated;

/// <summary>Leitor sem hardware: a tela de testes ou o simulador chamam <see cref="Emit"/>.</summary>
public sealed class SimulatedScanner : IBarcodeScanner
{
    public event EventHandler<string>? Scanned;

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public void Emit(string code) => Scanned?.Invoke(this, code);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class SimulatedScale : IScale
{
    public ScaleReading Next { get; set; } = new(ScaleStatus.Stable, 0);

    public Task<ScaleReading> ReadAsync(CancellationToken ct = default) => Task.FromResult(Next);
}

/// <summary>Guarda o que seria impresso como texto; útil para conferir layout de comprovante sem papel.</summary>
public sealed class SimulatedPrinter(int columns = 48) : IReceiptPrinter
{
    public List<string> Printed { get; } = [];
    public int DrawerOpenings { get; private set; }

    public Task PrintAsync(ReceiptDocument document, CancellationToken ct = default)
    {
        Printed.Add(RenderText(document, columns));
        return Task.CompletedTask;
    }

    public Task OpenDrawerAsync(CancellationToken ct = default)
    {
        DrawerOpenings++;
        return Task.CompletedTask;
    }

    public static string RenderText(ReceiptDocument document, int columns)
    {
        var sb = new StringBuilder();
        foreach (var line in document.Lines)
        {
            switch (line)
            {
                case ReceiptLine.Text t:
                    var text = t.Bold ? t.Value.ToUpperInvariant() : t.Value;
                    sb.AppendLine(t.Align switch
                    {
                        Align.Center => text.PadLeft((columns + text.Length) / 2),
                        Align.Right => text.PadLeft(columns),
                        _ => text,
                    });
                    break;
                case ReceiptLine.Columns c:
                    sb.AppendLine(c.Left + new string(' ', Math.Max(1, columns - c.Left.Length - c.Right.Length)) + c.Right);
                    break;
                case ReceiptLine.Separator:
                    sb.AppendLine(new string('-', columns));
                    break;
                case ReceiptLine.QrCode q:
                    sb.AppendLine("[QR] " + q.Data);
                    break;
                case ReceiptLine.Feed f:
                    for (var i = 0; i < f.Lines; i++) sb.AppendLine();
                    break;
            }
        }
        return sb.ToString();
    }
}

/// <summary>
/// TEF de mentira para desenvolvimento. Aprova tudo, exceto valores terminados em 13 centavos
/// (regra para testar recusa na tela).
/// </summary>
public sealed class SimulatedPaymentTerminal : IPaymentTerminal
{
    private int _nsu = 1000;

    public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        if (request.AmountCents % 100 == 13)
            return Task.FromResult(new PaymentResult(false, null, null, "SIMULADO", "Transação negada (simulado)", [], []));

        var nsu = Interlocked.Increment(ref _nsu).ToString("D6");
        string[] receipt = [$"SIMULADO {request.Method.ToUpperInvariant()}", $"NSU {nsu}", $"VALOR {request.AmountCents / 100m:N2}"];
        return Task.FromResult(new PaymentResult(true, nsu, $"A{nsu}", "SIMULADO", "Aprovada", receipt, receipt));
    }

    public Task<PaymentResult> ReverseAsync(string nsu, long amountCents, CancellationToken ct = default) =>
        Task.FromResult(new PaymentResult(true, nsu, null, "SIMULADO", "Estorno aprovado (simulado)", [], []));
}
