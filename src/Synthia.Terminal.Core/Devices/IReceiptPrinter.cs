namespace Synthia.Terminal.Core.Devices;

public enum Align { Left, Center, Right }

public abstract record ReceiptLine
{
    public sealed record Text(string Value, Align Align = Align.Left, bool Bold = false) : ReceiptLine;

    /// <summary>Texto à esquerda e valor alinhado à direita na mesma linha.</summary>
    public sealed record Columns(string Left, string Right) : ReceiptLine;

    public sealed record Separator : ReceiptLine;

    public sealed record QrCode(string Data) : ReceiptLine;

    public sealed record Feed(int Lines = 1) : ReceiptLine;
}

/// <summary>Documento a imprimir: DANFE NFC-e, comprovante TEF, leitura de turno...</summary>
public sealed record ReceiptDocument(IReadOnlyList<ReceiptLine> Lines, bool Cut = true);

/// <summary>Impressora térmica não fiscal. A gaveta de dinheiro fica ligada nela.</summary>
public interface IReceiptPrinter
{
    Task PrintAsync(ReceiptDocument document, CancellationToken ct = default);

    Task OpenDrawerAsync(CancellationToken ct = default);
}
