using Synthia.Contracts.Sync;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Devices;
using Synthia.Terminal.Core.Fiscal;
using Synthia.Terminal.Core.Sales;
using Synthia.Terminal.Core.Storage;
using Synthia.Terminal.Core.Sync;

namespace Synthia.Terminal.Tests;

public sealed class StoreProfileTests : IDisposable
{
    private readonly TestTerminal _t = new();

    private static StoreProfile Profile(long version = 1, string? logoHash = null, string cnpj = "11222333000181") =>
        new(Guid.Parse("0199f000-0000-7000-8000-000000000001"), "Mercado Bom Preço", "Bom Preço Ltda", cnpj, "123.456.789.110",
            "Rua A, 10 - Centro", "Volte sempre!", "#253C70", "#C8272E", logoHash, version);

    [Fact]
    public void Sem_sincronizar_o_cupom_usa_o_terminal_json()
    {
        Assert.Null(_t.Store.Profile);
        Assert.Equal("Loja do terminal.json", _t.Store.Receipt.Name);
        Assert.False(_t.Store.HasLogo);
    }

    [Fact]
    public async Task Cadastro_da_loja_chega_pelo_sync_e_vale_no_cupom_mantendo_o_cnpj_do_caixa()
    {
        _t.Server.Store = Profile(cnpj: "99888777000100");

        Assert.True(await _t.StorePuller.PullAsync());

        var receipt = _t.Store.Receipt;
        Assert.Equal("Mercado Bom Preço", receipt.Name);
        Assert.Equal("11.222.333/0001-81", receipt.Cnpj);
        Assert.Equal("Volte sempre!", receipt.ReceiptFooter);
        Assert.True(_t.Store.CnpjMismatch);
    }

    [Fact]
    public async Task Copia_sobrevive_ao_reinicio_e_ao_servidor_fora_do_ar()
    {
        _t.Server.Store = Profile(logoHash: "abc");
        _t.Server.StoreLogo = [1, 2, 3];
        await _t.StorePuller.PullAsync();

        _t.Server.Online = false;
        await Assert.ThrowsAsync<HttpRequestException>(() => _t.StorePuller.PullAsync());
        var reopened = new StoreProfileCache(_t.Db, new StoreSection(), _t.Store.LogoPath);

        Assert.Equal("Mercado Bom Preço", reopened.Receipt.Name);
        Assert.True(reopened.HasLogo);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(reopened.LogoPath));
    }

    [Fact]
    public async Task Logo_so_e_baixada_quando_o_hash_muda_e_some_quando_removida()
    {
        _t.Server.Store = Profile(1, "abc");
        _t.Server.StoreLogo = [1];
        await _t.StorePuller.PullAsync();
        Assert.False(await _t.StorePuller.PullAsync());

        _t.Server.Store = Profile(2, "abc");
        Assert.True(await _t.StorePuller.PullAsync());
        Assert.Equal(1, _t.Server.LogoDownloads);

        _t.Server.Store = Profile(3, "def");
        _t.Server.StoreLogo = [2];
        await _t.StorePuller.PullAsync();
        Assert.Equal(2, _t.Server.LogoDownloads);
        Assert.Equal([2], File.ReadAllBytes(_t.Store.LogoPath));

        _t.Server.Store = Profile(4);
        await _t.StorePuller.PullAsync();
        Assert.False(_t.Store.HasLogo);
        Assert.False(File.Exists(_t.Store.LogoPath));
    }

    [Fact]
    public async Task Cupom_impresso_traz_nome_ie_e_rodape_do_cadastro()
    {
        _t.Server.Store = Profile();
        await _t.StorePuller.PullAsync();
        var builder = new ReceiptBuilder(() => _t.Store.Receipt, "CX01", NfceEnvironment.Producao);

        var doc = builder.CashWithdrawal(5000, "cofre", "Maria", _t.Clock.GetUtcNow());
        var texts = doc.Lines.OfType<ReceiptLine.Text>().Select(l => l.Value).ToList();

        Assert.Equal("Mercado Bom Preço", texts[0]);
        Assert.Contains("IE 123.456.789.110", texts);
        Assert.Contains("CNPJ 11.222.333/0001-81", texts);
    }

    public void Dispose() => _t.Dispose();
}
