using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Synthia.Contracts.Sync;
using Synthia.Terminal.Core.Sync;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Synthia.Terminal.App.ViewModels;
using Synthia.Terminal.App.Views;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Sales;
using Synthia.Terminal.Devices.Simulated;

namespace Synthia.Terminal.App.Tests;

/// <summary>
/// A tela do caixa de verdade, sem monitor: digita como o operador e confere o que aparece.
/// Com SYNTHIA_SCREENSHOTS=pasta, salva as capturas usadas na documentação.
/// </summary>
public sealed class CashierScreenTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"synthia-ui-{Guid.NewGuid():N}.db");
    private readonly TerminalHost _host;
    private readonly MainWindow _window;
    private readonly MainWindowViewModel _vm;

    public CashierScreenTests()
    {
        var config = new TerminalConfig
        {
            Terminal = new TerminalSection { Id = "CX01", ServerUrl = "http://127.0.0.1:9", DatabasePath = _db },
            Devices = new DevicesSection
            {
                Scale = new ScaleSection { Model = ScaleModel.Simulated },
                Printer = new PrinterSection { Connection = PrinterConnection.Simulated },
            },
        };
        _host = TerminalHost.Build(["--demo"], config);
        _vm = new MainWindowViewModel(_host);
        _window = new MainWindow { DataContext = _vm, Width = 1280, Height = 800, WindowState = WindowState.Normal };
        _window.Show();
        Pump();
    }

    public void Dispose()
    {
        _window.Close();
        _host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _db, _db + "-wal", _db + "-shm", _db + ".logo" }) File.Delete(f);
    }

    private static void Pump()
    {
        for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
    }

    private void Type(string text)
    {
        _window.KeyTextInput(text);
        Pump();
    }

    private void Press(Key key)
    {
        _window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        _window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Pump();
    }

    private void Snapshot(string name)
    {
        var dir = Environment.GetEnvironmentVariable("SYNTHIA_SCREENSHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        _window.CaptureRenderedFrame()?.Save(Path.Combine(dir, $"{name}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private SaleViewModel OpenShift()
    {
        var open = Assert.IsType<OpenShiftViewModel>(_vm.Screen);
        open.OperatorName = "Maria";
        Pump();
        Snapshot("caixa-1-abrir-turno");
        Press(Key.Enter);
        return Assert.IsType<SaleViewModel>(_vm.Screen);
    }

    [AvaloniaFact]
    public void Nome_logo_e_cores_da_loja_chegam_na_tela_quando_o_cadastro_sincroniza()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var defaultLogo = StoreBrand.Current.Logo;
        var profile = new StoreProfile(Guid.NewGuid(), "Mercado da Esquina", "", "", "", "", "", "#114477", "#AA2200", "hash1", 1);

        _host.Get<StoreProfileCache>().Apply(profile, png);
        Pump();

        Assert.Equal("Mercado da Esquina", StoreBrand.Current.StoreName);
        Assert.NotSame(defaultLogo, StoreBrand.Current.Logo);
        Assert.Equal(Color.Parse("#114477"), ((SolidColorBrush)Application.Current!.Resources["NavyBrush"]!).Color);
        Assert.Equal(Color.Parse("#AA2200"), ((SolidColorBrush)Application.Current!.Resources["RedBrush"]!).Color);
    }

    [AvaloniaFact]
    public void Caixa_fechado_abre_turno_com_Enter()
    {
        var sale = OpenShift();

        Assert.Equal("Maria", _vm.OperatorName);
        Assert.Empty(sale.Items);
    }

    [AvaloniaFact]
    public void Venda_completa_pelo_teclado()
    {
        var sale = OpenShift();

        Type("2*7891000100103"); Press(Key.Enter);   // 2 arroz
        Type("1001"); Press(Key.Enter);              // banana na balança simulada (0 kg → pede para pesar)
        Assert.Equal(FeedbackKind.Warning, sale.MessageKind);

        Type("1,235*1001"); Press(Key.Enter);        // peso digitado
        Type("2001002004002"); Press(Key.Enter);     // etiqueta com DV errado → recusada
        Type("7891000400401"); Press(Key.Enter);     // café
        Snapshot("caixa-2-venda");

        Assert.Equal(3, sale.Items.Count);
        Assert.Equal("R$ 84,37", sale.Total);       // 2 × 28,99 + 7,40 + 18,99

        Press(Key.F4);
        Assert.True(sale.IsPaying);
        Type("50"); Press(Key.F6);                   // R$ 50 no débito
        Snapshot("caixa-3-pagamento");
        Assert.Equal("R$ 34,37", sale.Remaining);

        Type("40"); Press(Key.Enter);                // R$ 40 em dinheiro
        Snapshot("caixa-4-finalizada");

        Assert.Equal(FeedbackKind.Success, sale.MessageKind);
        Assert.Contains("TROCO R$ 5,63", sale.Message);
        Assert.Empty(sale.Items);
        Assert.False(sale.IsPaying);

        var printer = Assert.IsType<SimulatedPrinter>(_host.Get<Synthia.Terminal.Core.Devices.IReceiptPrinter>());
        Assert.Contains("Arroz Tipo 1 5kg", printer.Printed[0]);
        Assert.Equal(1, printer.DrawerOpenings);
    }

    [AvaloniaFact]
    public void Esc_duas_vezes_descarta_a_venda()
    {
        var sale = OpenShift();
        Type("7891000100103"); Press(Key.Enter);

        Press(Key.Escape);
        Assert.Single(sale.Items);
        Press(Key.Escape);

        Assert.Empty(sale.Items);
    }

    [AvaloniaFact]
    public void Sangria_e_fechamento_de_turno()
    {
        var sale = OpenShift();

        Press(Key.F9); Type("30"); Press(Key.Enter);
        Assert.Equal(FeedbackKind.Success, sale.MessageKind);

        Press(Key.F12);
        Assert.Equal(PromptMode.CloseShift, sale.Mode);
        Type("70"); Press(Key.Enter);

        Assert.IsType<OpenShiftViewModel>(_vm.Screen);
        Assert.Null(_host.Get<ShiftService>().CurrentShiftId());
    }
}
