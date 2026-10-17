using System.Text;
using Synthia.Terminal.Core.Devices;

namespace Synthia.Terminal.Devices.Scale;

/// <summary>
/// Protocolo serial Toledo de balança de checkout (Prix 3 Fit e similares; muitas Filizola e Urano têm
/// "modo Toledo"). O caixa envia ENQ; a balança responde STX + 5 caracteres + ETX:
/// peso em gramas ("01235" = 1,235 kg), "IIIII" instável, "NNNNN" negativo, "SSSSS" sobrecarga.
/// Confirmar no manual do modelo instalado: alguns usam 6 dígitos ou outra unidade.
/// </summary>
public static class ToledoProtocol
{
    public const byte Enq = 0x05;
    public const byte Stx = 0x02;
    public const byte Etx = 0x03;

    public static readonly byte[] WeightRequest = [Enq];

    public static ScaleReading Parse(ReadOnlySpan<byte> response)
    {
        var start = response.IndexOf(Stx);
        var end = response.LastIndexOf(Etx);
        if (start < 0 || end <= start + 1) return ScaleReading.NoResponse;

        var body = Encoding.ASCII.GetString(response[(start + 1)..end]).Trim();
        if (body.Length == 0) return ScaleReading.NoResponse;

        return body[0] switch
        {
            'I' => new ScaleReading(ScaleStatus.Unstable, 0),
            'N' => new ScaleReading(ScaleStatus.Negative, 0),
            'S' => new ScaleReading(ScaleStatus.Overload, 0),
            _ when body.All(char.IsAsciiDigit) => new ScaleReading(ScaleStatus.Stable, int.Parse(body) / 1000m),
            _ => ScaleReading.NoResponse,
        };
    }
}
