using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Devices;
using Synthia.Terminal.Devices;
using Synthia.Terminal.Devices.Printing;
using Synthia.Terminal.Devices.Scale;
using Synthia.Terminal.Devices.Serial;
using Synthia.Terminal.Devices.Simulated;

namespace Synthia.Terminal.Tests;

public sealed class DeviceTests
{
    private static byte[] Frame(string body) => [0x02, .. Encoding.ASCII.GetBytes(body), 0x03];

    [Theory]
    [InlineData("01235", ScaleStatus.Stable, 1.235)]
    [InlineData("00000", ScaleStatus.Stable, 0)]
    [InlineData("IIIII", ScaleStatus.Unstable, 0)]
    [InlineData("NNNNN", ScaleStatus.Negative, 0)]
    [InlineData("SSSSS", ScaleStatus.Overload, 0)]
    [InlineData("12x45", ScaleStatus.NoResponse, 0)]
    public void Protocolo_Toledo_interpreta_resposta(string body, ScaleStatus status, double kg)
    {
        var reading = ToledoProtocol.Parse(Frame(body));

        Assert.Equal(status, reading.Status);
        Assert.Equal((decimal)kg, reading.Kg);
    }

    [Fact]
    public void Resposta_sem_moldura_e_ausencia_de_resposta() =>
        Assert.Equal(ScaleStatus.NoResponse, ToledoProtocol.Parse("01235"u8).Status);

    [Fact]
    public async Task Balanca_serial_pede_peso_com_ENQ()
    {
        var channel = new FakeChannel { Response = Frame("00740") };

        var reading = await new SerialScale(channel).ReadAsync();

        Assert.Equal([0x05], channel.Written.Single());
        Assert.True(reading.IsUsable);
        Assert.Equal(0.74m, reading.Kg);
    }

    [Fact]
    public async Task Balanca_desconectada_nao_derruba_a_venda()
    {
        Assert.Equal(ScaleStatus.NoResponse, (await new SerialScale(new FakeChannel { Response = null }).ReadAsync()).Status);
        Assert.Equal(ScaleStatus.NoResponse, (await new SerialScale(new FakeChannel { Throw = true }).ReadAsync()).Status);
    }

    [Fact]
    public void EscPos_inicializa_codifica_acentos_e_corta()
    {
        var doc = new ReceiptDocument([new ReceiptLine.Text("Feijão, açúcar", Align.Center, Bold: true)]);

        var bytes = EscPos.Render(doc);

        Assert.Equal(EscPos.Initialize, bytes[..EscPos.Initialize.Length]);
        Assert.Contains((byte)0xC6, bytes); // ã em CP850
        Assert.Contains((byte)0x87, bytes); // ç em CP850
        Assert.Equal(EscPos.PartialCut, bytes[^EscPos.PartialCut.Length..]);
    }

    [Fact]
    public void Colunas_ocupam_exatamente_a_largura_da_bobina()
    {
        var doc = new ReceiptDocument([new ReceiptLine.Columns("1,235 kg x Banana prata muito comprida demais para caber", "R$ 7,40")], Cut: false);

        var text = Encoding.Latin1.GetString(EscPos.Render(doc, 48)[EscPos.Initialize.Length..^1]);

        Assert.Equal(48, text.Length);
        Assert.EndsWith("R$ 7,40", text);
    }

    [Fact]
    public void QrCode_grava_o_conteudo_com_tamanho_correto()
    {
        const string url = "https://www.nfce.fazenda.sp.gov.br/qrcode?p=35261000000000000000650010000000011";

        var bytes = EscPos.QrCode(url);

        var store = Encoding.ASCII.GetBytes(url).Length + 3;
        var header = new byte[] { 0x1D, (byte)'(', (byte)'k', (byte)(store % 256), (byte)(store / 256), 49, 80, 48 };
        Assert.True(bytes.AsSpan().IndexOf(header) >= 0);
    }

    [Fact]
    public void Texto_longo_quebra_por_palavra() =>
        Assert.Equal(["CONSUMIDOR NAO", "IDENTIFICADO"], EscPos.Wrap("CONSUMIDOR NAO IDENTIFICADO", 15).ToArray());

    [Fact]
    public async Task Gaveta_abre_pela_impressora()
    {
        var transport = new FakeTransport();

        await new EscPosPrinter(transport).OpenDrawerAsync();

        Assert.Equal(EscPos.DrawerPulse, transport.Sent.Single());
    }

    [Fact]
    public void Leitor_em_modo_teclado_e_reconhecido_pela_velocidade()
    {
        var d = new KeyboardWedgeDetector();
        var t = TimeSpan.Zero;
        string? result = null;

        foreach (var c in "7891000100103\r")
        {
            t += TimeSpan.FromMilliseconds(8);
            result = d.Feed(c, t);
        }

        Assert.Equal("7891000100103", result);
    }

    [Fact]
    public void Digitacao_humana_nao_vira_leitura()
    {
        var d = new KeyboardWedgeDetector();
        var t = TimeSpan.Zero;
        string? result = null;

        foreach (var c in "1001\r")
        {
            t += TimeSpan.FromMilliseconds(180);
            result = d.Feed(c, t);
        }

        Assert.Null(result);
    }

    [Fact]
    public void Leitura_depois_de_digitacao_lenta_ainda_e_reconhecida()
    {
        var d = new KeyboardWedgeDetector();
        d.Feed('9', TimeSpan.FromMilliseconds(0));
        d.Feed('9', TimeSpan.FromMilliseconds(400));
        var t = TimeSpan.FromSeconds(2);
        string? result = null;
        foreach (var c in "7891000100103\r") result = d.Feed(c, t += TimeSpan.FromMilliseconds(5));

        Assert.Equal("7891000100103", result);
    }

    [Fact]
    public void Exemplo_de_terminal_json_e_valido()
    {
        var config = TerminalConfig.Load(Path.Combine(RepoRoot(), "config", "terminal.example.json"));

        Assert.Empty(config.Validate());
        Assert.Equal(ScaleModel.Toledo, config.Devices.Scale.Model);
        Assert.Equal(PrinterConnection.Network, config.Devices.Printer.Connection);
        Assert.Equal(6, config.ToScaleBarcodeFormat().CodeLength);
    }

    [Fact]
    public void Configuracao_vazia_usa_padroes_de_mercado()
    {
        var config = TerminalConfig.Parse("{}");

        Assert.Equal(ScannerMode.Keyboard, config.Devices.Scanner.Mode);
        Assert.Equal(48, config.Devices.Printer.Columns);
        Assert.Equal(NfceEnvironment.Homologacao, config.Nfce.Ambiente);
        Assert.Empty(config.Validate());
    }

    [Fact]
    public void Configuracao_invalida_explica_cada_problema()
    {
        var config = TerminalConfig.Parse("""
            { "terminal": { "id": "caixa1" },
              "nfce": { "serie": 0 },
              "devices": { "scale": { "model": "toledo" }, "printer": { "connection": "network", "columns": 50 } } }
            """);

        var errors = config.Validate();

        Assert.Equal(5, errors.Count);
    }

    [Fact]
    public void Fabrica_monta_dispositivos_simulados_e_recusa_TEF_nao_integrado()
    {
        var devices = TerminalConfig.Parse("""
            { "devices": { "scanner": { "mode": "simulated" }, "scale": { "model": "simulated" },
                           "printer": { "connection": "simulated" }, "paymentTerminal": { "provider": "sitef" } } }
            """).Devices;
        var factory = new DeviceFactory(devices, NullLoggerFactory.Instance);

        Assert.IsType<SimulatedScanner>(factory.CreateScanner());
        Assert.IsType<SimulatedScale>(factory.CreateScale());
        Assert.IsType<SimulatedPrinter>(factory.CreatePrinter());
        Assert.Throws<NotSupportedException>(factory.CreatePaymentTerminal);
        Assert.Null(new DeviceFactory(new DevicesSection(), NullLoggerFactory.Instance).CreateScanner()); // teclado
    }

    [Fact]
    public async Task TEF_simulado_aprova_e_nega_por_regra()
    {
        var tef = new SimulatedPaymentTerminal();

        var ok = await tef.AuthorizeAsync(new PaymentRequest(Guid.NewGuid(), "debit", 2847));
        var denied = await tef.AuthorizeAsync(new PaymentRequest(Guid.NewGuid(), "debit", 2813));

        Assert.True(ok.Approved);
        Assert.NotNull(ok.Nsu);
        Assert.False(denied.Approved);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PdvSynthia.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    private sealed class FakeChannel : ISerialChannel
    {
        public byte[]? Response { get; init; }
        public bool Throw { get; init; }
        public List<byte[]> Written { get; } = [];

        public Task WriteAsync(byte[] data, CancellationToken ct = default)
        {
            if (Throw) throw new IOException("porta não existe");
            Written.Add(data);
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadUntilAsync(byte terminator, TimeSpan timeout, CancellationToken ct = default) =>
            Task.FromResult(Response);

        public void Dispose() { }
    }

    private sealed class FakeTransport : IPrinterTransport
    {
        public List<byte[]> Sent { get; } = [];

        public Task SendAsync(byte[] data, CancellationToken ct = default)
        {
            Sent.Add(data);
            return Task.CompletedTask;
        }
    }
}
