using System.IO.Ports;

namespace Synthia.Terminal.Devices.Serial;

/// <param name="portName">COM3 no Windows; /dev/ttyUSB0 no Linux (adaptador USB-serial).</param>
public sealed class SerialPortChannel(string portName, int baudRate = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    : ISerialChannel
{
    private readonly SerialPort _port = new(portName, baudRate, parity, dataBits, stopBits);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private SerialPort Port
    {
        get
        {
            if (!_port.IsOpen) _port.Open();
            return _port;
        }
    }

    public async Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Port.DiscardInBuffer();
            await Port.BaseStream.WriteAsync(data, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<byte[]?> ReadUntilAsync(byte terminator, TimeSpan timeout, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            var buffer = new List<byte>();
            var one = new byte[1];
            try
            {
                while (await Port.BaseStream.ReadAsync(one, cts.Token) == 1)
                {
                    buffer.Add(one[0]);
                    if (one[0] == terminator) return [.. buffer];
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            return null;
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        _port.Dispose();
        _gate.Dispose();
    }
}
