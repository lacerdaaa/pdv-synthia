namespace Synthia.Contracts;

/// <summary>
/// UUIDv7 estritamente crescente no processo. <see cref="Guid.CreateVersion7()"/> sozinho
/// é aleatório dentro do mesmo milissegundo: uma venda e o cancelamento dela poderiam sair
/// fora de ordem na outbox (ordenada por event_id).
/// </summary>
public static class Uuid7
{
    private static readonly Lock Gate = new();
    private static Guid _last;

    public static Guid Next() => Next(DateTimeOffset.UtcNow);

    public static Guid Next(DateTimeOffset timestamp)
    {
        var candidate = Guid.CreateVersion7(timestamp);
        lock (Gate)
        {
            if (candidate.CompareTo(_last) <= 0)
                candidate = Increment(_last);
            _last = candidate;
            return candidate;
        }
    }

    // Soma 1 na parte aleatória final (rand_b, bytes 9..15), preservando versão e variante.
    private static Guid Increment(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        for (var i = 15; i >= 9; i--)
        {
            if (++bytes[i] != 0) break;
        }
        return new Guid(bytes, bigEndian: true);
    }
}
