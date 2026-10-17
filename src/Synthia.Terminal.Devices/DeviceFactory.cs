using Microsoft.Extensions.Logging;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Devices;
using Synthia.Terminal.Devices.Printing;
using Synthia.Terminal.Devices.Scale;
using Synthia.Terminal.Devices.Scanning;
using Synthia.Terminal.Devices.Serial;
using Synthia.Terminal.Devices.Simulated;

namespace Synthia.Terminal.Devices;

/// <summary>Monta os dispositivos do caixa a partir do <c>terminal.json</c>.</summary>
public sealed class DeviceFactory(DevicesSection config, ILoggerFactory loggers)
{
    /// <summary>Nulo no modo teclado: a interface usa <see cref="KeyboardWedgeDetector"/>.</summary>
    public IBarcodeScanner? CreateScanner() => config.Scanner.Mode switch
    {
        ScannerMode.Serial => new SerialBarcodeScanner(
            new SerialPortChannel(config.Scanner.Port!, config.Scanner.BaudRate), loggers.CreateLogger<SerialBarcodeScanner>()),
        ScannerMode.Simulated => new SimulatedScanner(),
        _ => null,
    };

    /// <summary>Nulo quando o caixa não tem balança: o peso é digitado.</summary>
    public IScale? CreateScale() => config.Scale.Model switch
    {
        ScaleModel.Toledo => new SerialScale(new SerialPortChannel(config.Scale.Port!, config.Scale.BaudRate)),
        ScaleModel.Simulated => new SimulatedScale(),
        _ => null,
    };

    public IReceiptPrinter CreatePrinter()
    {
        var p = config.Printer;
        return p.Connection switch
        {
            PrinterConnection.Network => new EscPosPrinter(NetworkTransport(p.Address!), p.Columns),
            PrinterConnection.Serial => new EscPosPrinter(new SerialPrinterTransport(new SerialPortChannel(p.Address!, p.BaudRate)), p.Columns),
            PrinterConnection.Device => new EscPosPrinter(new DevicePrinterTransport(p.Address!), p.Columns),
            _ => new SimulatedPrinter(p.Columns),
        };
    }

    public IPaymentTerminal CreatePaymentTerminal() => config.PaymentTerminal.Provider switch
    {
        PaymentProvider.Simulated => new SimulatedPaymentTerminal(),
        var provider => throw new NotSupportedException(
            $"TEF {provider} ainda não integrado. Use \"simulated\" até a homologação com o provedor."),
    };

    private static NetworkPrinterTransport NetworkTransport(string address)
    {
        var parts = address.Split(':', 2);
        return new NetworkPrinterTransport(parts[0], parts.Length == 2 ? int.Parse(parts[1]) : 9100);
    }
}
