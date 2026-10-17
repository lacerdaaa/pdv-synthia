namespace Synthia.Terminal.Core.Devices;

public enum ScaleStatus
{
    Stable,
    /// <summary>Peso oscilando: o operador espera estabilizar.</summary>
    Unstable,
    Overload,
    Negative,
    NoResponse,
}

public sealed record ScaleReading(ScaleStatus Status, decimal Kg)
{
    public static readonly ScaleReading NoResponse = new(ScaleStatus.NoResponse, 0);

    public bool IsUsable => Status == ScaleStatus.Stable && Kg > 0;
}

/// <summary>Balança de checkout ligada ao caixa (não confundir com a etiquetadora do açougue).</summary>
public interface IScale
{
    Task<ScaleReading> ReadAsync(CancellationToken ct = default);
}
