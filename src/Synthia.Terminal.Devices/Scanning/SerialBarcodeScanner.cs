using Microsoft.Extensions.Logging;
using Synthia.Terminal.Core.Devices;
using Synthia.Terminal.Devices.Serial;

namespace Synthia.Terminal.Devices.Scanning;

/// <summary>
/// Leitor configurado em modo serial (USB-COM): cada leitura chega como texto terminado em CR.
/// Mais robusto que o modo teclado porque não depende de qual campo da tela está com foco.
/// </summary>
public sealed class SerialBarcodeScanner(ISerialChannel channel, ILogger<SerialBarcodeScanner> logger) : IBarcodeScanner
{
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event EventHandler<string>? Scanned;

    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(() => LoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var data = await channel.ReadUntilAsync((byte)'\r', TimeSpan.FromSeconds(30), ct);
                if (data is null) continue;

                var code = System.Text.Encoding.ASCII.GetString(data).Trim('\r', '\n', ' ', '\0');
                if (code.Length > 0) Scanned?.Invoke(this, code);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Leitor serial indisponível; nova tentativa em 2 s");
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null) await _cts.CancelAsync();
        if (_loop is not null) await _loop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _cts?.Dispose();
        channel.Dispose();
    }
}
