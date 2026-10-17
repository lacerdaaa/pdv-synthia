using Synthia.Contracts;

namespace Synthia.Terminal.Tests;

public sealed class Uuid7Tests
{
    [Fact]
    public void Ids_no_mesmo_milissegundo_saem_em_ordem_crescente()
    {
        var at = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0, 10_000).Select(_ => Uuid7.Next(at)).ToList();

        Assert.Equal(ids.Order().ToList(), ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Equal(7, id.Version));
        // A ordem textual (usada pelo SQLite e pelo Postgres) bate com a do Guid.
        Assert.Equal(ids.Select(i => i.ToString()).Order(StringComparer.Ordinal).ToList(), ids.Select(i => i.ToString()).ToList());
    }
}
