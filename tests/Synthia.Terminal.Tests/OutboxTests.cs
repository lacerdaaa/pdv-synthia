using Dapper;
using Synthia.Contracts.Events;

namespace Synthia.Terminal.Tests;

public sealed class OutboxTests : IDisposable
{
    private readonly TestTerminal _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Servidor_fora_acumula_e_drena_quando_volta()
    {
        _t.Server.Online = false;
        _t.Shifts.Open("Maria", 0);
        for (var i = 0; i < 5; i++) _t.Shifts.Withdraw(100, "teste");

        await Assert.ThrowsAsync<HttpRequestException>(() => _t.Pusher.PushPendingAsync());
        Assert.Equal(6, _t.Pending());

        _t.Server.Online = true;
        Assert.Equal(6, await _t.Pusher.PushPendingAsync());
        Assert.Equal(0, _t.Pending());
        Assert.Equal(6, _t.Server.Events.Count);
    }

    [Fact]
    public async Task Envia_em_lotes_de_100_em_ordem_de_event_id()
    {
        _t.Shifts.Open("Maria", 0);
        for (var i = 0; i < 249; i++) _t.Shifts.Withdraw(1, "lote");

        await _t.Pusher.PushPendingAsync();

        Assert.Equal(3, _t.Server.PushCalls);
        var ids = _t.Server.Events.Keys.ToList();
        Assert.Equal(ids.Order().ToList(), ids);
    }

    [Fact]
    public async Task Evento_registra_versao_do_catalogo_em_uso()
    {
        _t.SeedProduct("Pão", 99, version: 42);
        _t.Shifts.Open("Maria", 0);
        await _t.Pusher.PushPendingAsync();

        Assert.Equal(42, _t.Server.Events.Values.Single(e => e.Type == EventTypes.ShiftOpened).CatalogVersion);
    }

    [Fact]
    public async Task Poda_so_remove_enviados_ha_mais_de_30_dias()
    {
        _t.Shifts.Open("Maria", 0);
        await _t.Pusher.PushPendingAsync();
        _t.Shifts.Withdraw(100, "não enviado");

        _t.Clock.Advance(TimeSpan.FromDays(31));
        using var conn = _t.Db.Open();
        Assert.Equal(1, _t.Outbox.Prune(conn));
        Assert.Equal(1, conn.ExecuteScalar<int>("SELECT count(*) FROM outbox"));
        Assert.Equal(1, _t.Pending());
    }
}
