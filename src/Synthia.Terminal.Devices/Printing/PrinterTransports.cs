using System.Net.Sockets;
using Synthia.Terminal.Devices.Serial;

namespace Synthia.Terminal.Devices.Printing;

public interface IPrinterTransport
{
    Task SendAsync(byte[] data, CancellationToken ct = default);
}

/// <summary>Impressora em rede: porta RAW 9100, padrão das térmicas com Ethernet.</summary>
public sealed class NetworkPrinterTransport(string host, int port = 9100) : IPrinterTransport
{
    public async Task SendAsync(byte[] data, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(host, port, cts.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(data, cts.Token);
    }
}

public sealed class SerialPrinterTransport(ISerialChannel channel) : IPrinterTransport
{
    public Task SendAsync(byte[] data, CancellationToken ct = default) => channel.WriteAsync(data, ct);
}

/// <summary>
/// Escrita direta no dispositivo: <c>/dev/usb/lp0</c> no Linux, ou o caminho de uma impressora
/// compartilhada com driver "Generic / Text Only" no Windows (<c>\\localhost\Termica</c>).
/// </summary>
public sealed class DevicePrinterTransport(string path) : IPrinterTransport
{
    public async Task SendAsync(byte[] data, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        await stream.WriteAsync(data, ct);
    }
}
