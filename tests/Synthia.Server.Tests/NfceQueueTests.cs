using Microsoft.Extensions.DependencyInjection;
using Synthia.Contracts;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;
using Synthia.Server.Backoffice;
using Synthia.Server.Catalog;
using Synthia.Server.Sync;
using static Synthia.Server.Tests.EventBuilder;

namespace Synthia.Server.Tests;

public sealed class NfceQueueTests : IAsyncLifetime
{
    private readonly ServerFixture fx = new();

    public Task InitializeAsync() => fx.InitializeAsync();
    public Task DisposeAsync() => fx.DisposeAsync();

    private T Get<T>() where T : notnull => fx.Factory.Services.GetRequiredService<T>();

    private async Task<SaleCompleted> SaleAsync(ProductData product, string nfceStatus, DateTimeOffset at)
    {
        var (e, sale) = Sale(Guid.CreateVersion7(), (product.Id, 1, product.PriceCents));
        sale = sale with { NfceKey = $"35261000000000000000650010000000{Random.Shared.Next(100000, 999999)}1000000001", NfceStatus = nfceStatus };
        await Get<EventStore>().AppendAsync([e with { OccurredAt = at, Payload = SynthiaJson.ToElement(sale) }]);
        return sale;
    }

    [Fact]
    public async Task Fila_mostra_contingencia_com_prazo_e_rejeitada_com_suspeito_de_ncm()
    {
        var semNcm = new ProductData(Guid.CreateVersion7(), "Biscoito", "7890000000017", null, "UN", 450, "00000000", true);
        var ok = new ProductData(Guid.CreateVersion7(), "Leite", "7890000000024", null, "UN", 549, "04012010", true);
        await Get<CatalogService>().UpsertProductsAsync([semNcm, ok]);

        var antiga = await SaleAsync(ok, NfceStatus.Pending, DateTimeOffset.UtcNow.AddHours(-30));
        var recente = await SaleAsync(ok, NfceStatus.Pending, DateTimeOffset.UtcNow.AddHours(-1));
        var rejeitada = await SaleAsync(semNcm, NfceStatus.Authorized, DateTimeOffset.UtcNow.AddMinutes(-10));
        await Get<EventStore>().AppendAsync([Make(EventTypes.NfceRejected, rejeitada.SaleId, 2,
            new NfceRejected(rejeitada.SaleId, rejeitada.NfceKey!, "778", "Informado NCM inexistente"))]);

        var queue = Get<NfceQueueService>();
        var list = await queue.ListAsync();
        var now = DateTime.UtcNow;

        Assert.Equal(3, list.Count);
        Assert.Equal(NfceStatus.Rejected, list[0].Status); // rejeitadas primeiro
        Assert.Equal("778: Informado NCM inexistente", list[0].Reason);
        Assert.Equal(semNcm.Id, Assert.Single(list[0].SuspectLines).ProductId);
        Assert.True(list.Single(e => e.SaleId == antiga.SaleId).Overdue(now));
        Assert.False(list.Single(e => e.SaleId == recente.SaleId).Overdue(now));
        Assert.Equal(new NfceQueueSummary(2, 1, 1), await queue.SummaryAsync());
    }

    [Fact]
    public async Task Autorizacao_tira_da_fila()
    {
        var p = new ProductData(Guid.CreateVersion7(), "Pão", null, 1002, "KG", 1690, "19059090", true);
        await Get<CatalogService>().UpsertProductAsync(p);
        var sale = await SaleAsync(p, NfceStatus.Pending, DateTimeOffset.UtcNow);

        await Get<EventStore>().AppendAsync([Make(EventTypes.NfceAuthorized, sale.SaleId, 2, new NfceAuthorized(sale.SaleId, sale.NfceKey!, "1352"))]);

        Assert.Empty(await Get<NfceQueueService>().ListAsync());
    }
}
