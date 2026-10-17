namespace Synthia.Terminal.Devices.Serial;

/// <summary>Canal de bytes com o equipamento; separa o protocolo do transporte para poder testar sem hardware.</summary>
public interface ISerialChannel : IDisposable
{
    Task WriteAsync(byte[] data, CancellationToken ct = default);

    /// <summary>Lê até encontrar <paramref name="terminator"/> (incluso) ou estourar o tempo; nulo em timeout.</summary>
    Task<byte[]?> ReadUntilAsync(byte terminator, TimeSpan timeout, CancellationToken ct = default);
}
