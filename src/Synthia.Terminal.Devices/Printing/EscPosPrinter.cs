using Synthia.Terminal.Core.Devices;

namespace Synthia.Terminal.Devices.Printing;

/// <param name="columns">48 para bobina de 80 mm com fonte A; 32 para 58 mm.</param>
public sealed class EscPosPrinter(IPrinterTransport transport, int columns = 48) : IReceiptPrinter
{
    public Task PrintAsync(ReceiptDocument document, CancellationToken ct = default) =>
        transport.SendAsync(EscPos.Render(document, columns), ct);

    public Task OpenDrawerAsync(CancellationToken ct = default) =>
        transport.SendAsync(EscPos.DrawerPulse, ct);
}
