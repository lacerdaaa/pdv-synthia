using Synthia.Terminal.Core.Devices;
using Synthia.Terminal.Devices.Serial;

namespace Synthia.Terminal.Devices.Scale;

/// <summary>Balança de checkout no protocolo Toledo sobre um canal serial.</summary>
public sealed class SerialScale(ISerialChannel channel, TimeSpan? timeout = null) : IScale
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMilliseconds(800);

    public async Task<ScaleReading> ReadAsync(CancellationToken ct = default)
    {
        try
        {
            await channel.WriteAsync(ToledoProtocol.WeightRequest, ct);
            var response = await channel.ReadUntilAsync(ToledoProtocol.Etx, _timeout, ct);
            return response is null ? ScaleReading.NoResponse : ToledoProtocol.Parse(response);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Cabo solto, porta ocupada ou inexistente: a venda segue com o peso digitado.
            return ScaleReading.NoResponse;
        }
    }
}
