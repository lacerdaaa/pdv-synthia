using System.Text;
using Synthia.Terminal.Core.Devices;

namespace Synthia.Terminal.Devices.Printing;

/// <summary>
/// Converte um <see cref="ReceiptDocument"/> em comandos ESC/POS, o padrão de fato das térmicas
/// (Epson, Elgin, Bematech, Daruma, Tanca...). Página de código 850 cobre os acentos do português.
/// </summary>
public static class EscPos
{
    private const byte Esc = 0x1B, Gs = 0x1D, Lf = 0x0A;

    private static readonly Encoding Cp850;

    static EscPos()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp850 = Encoding.GetEncoding(850);
    }

    public static readonly byte[] Initialize = [Esc, (byte)'@', Esc, (byte)'t', 2]; // reset + PC850
    public static readonly byte[] PartialCut = [Gs, (byte)'V', 66, 3];               // avança 3 linhas e corta
    public static readonly byte[] DrawerPulse = [Esc, (byte)'p', 0, 25, 250];        // pino 2, 50 ms on / 500 ms off

    public static byte[] Render(ReceiptDocument document, int columns = 48)
    {
        var buf = new List<byte>(1024);
        buf.AddRange(Initialize);

        foreach (var line in document.Lines)
        {
            switch (line)
            {
                case ReceiptLine.Text t:
                    buf.AddRange([Esc, (byte)'a', (byte)t.Align]);
                    if (t.Bold) buf.AddRange([Esc, (byte)'E', 1]);
                    foreach (var wrapped in Wrap(t.Value, columns)) AddLine(buf, wrapped);
                    if (t.Bold) buf.AddRange([Esc, (byte)'E', 0]);
                    buf.AddRange([Esc, (byte)'a', 0]);
                    break;

                case ReceiptLine.Columns c:
                    // Corta o texto da esquerda para o valor da direita sempre caber na linha.
                    var left = c.Left[..Math.Min(c.Left.Length, Math.Max(0, columns - c.Right.Length - 1))];
                    AddLine(buf, left + new string(' ', Math.Max(1, columns - left.Length - c.Right.Length)) + c.Right);
                    break;

                case ReceiptLine.Separator:
                    AddLine(buf, new string('-', columns));
                    break;

                case ReceiptLine.QrCode q:
                    buf.AddRange([Esc, (byte)'a', 1]);
                    buf.AddRange(QrCode(q.Data));
                    buf.AddRange([Esc, (byte)'a', 0]);
                    break;

                case ReceiptLine.Feed f:
                    buf.AddRange([Esc, (byte)'d', (byte)Math.Clamp(f.Lines, 1, 255)]);
                    break;
            }
        }

        if (document.Cut) buf.AddRange(PartialCut);
        return [.. buf];
    }

    /// <summary>QR Code nativo (GS ( k): modelo 2, módulo 5, correção M. Usado no DANFE NFC-e.</summary>
    public static byte[] QrCode(string data)
    {
        var bytes = Encoding.ASCII.GetBytes(data);
        var len = bytes.Length + 3;
        return
        [
            Gs, (byte)'(', (byte)'k', 4, 0, 49, 65, 50, 0,                       // modelo 2
            Gs, (byte)'(', (byte)'k', 3, 0, 49, 67, 5,                           // tamanho do módulo
            Gs, (byte)'(', (byte)'k', 3, 0, 49, 69, 49,                          // correção de erro M
            Gs, (byte)'(', (byte)'k', (byte)(len % 256), (byte)(len / 256), 49, 80, 48, .. bytes, // grava
            Gs, (byte)'(', (byte)'k', 3, 0, 49, 81, 48,                          // imprime
            Lf,
        ];
    }

    private static void AddLine(List<byte> buf, string text)
    {
        buf.AddRange(Cp850.GetBytes(text));
        buf.Add(Lf);
    }

    internal static IEnumerable<string> Wrap(string text, int columns)
    {
        if (text.Length == 0) { yield return ""; yield break; }

        var line = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            var w = word;
            while (w.Length > columns)
            {
                if (line.Length > 0) { yield return line.ToString(); line.Clear(); }
                yield return w[..columns];
                w = w[columns..];
            }

            if (line.Length > 0 && line.Length + 1 + w.Length > columns) { yield return line.ToString(); line.Clear(); }
            if (line.Length > 0) line.Append(' ');
            line.Append(w);
        }

        if (line.Length > 0) yield return line.ToString();
    }
}
