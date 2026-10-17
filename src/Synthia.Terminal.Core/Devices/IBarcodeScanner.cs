namespace Synthia.Terminal.Core.Devices;

/// <summary>
/// Leitor de código de barras. No modo serial o próprio driver dispara <see cref="Scanned"/>;
/// no modo teclado (HID) a interface alimenta um <see cref="KeyboardWedgeDetector"/>.
/// </summary>
public interface IBarcodeScanner : IAsyncDisposable
{
    event EventHandler<string>? Scanned;

    Task StartAsync(CancellationToken ct = default);
}
