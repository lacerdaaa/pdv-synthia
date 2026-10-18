using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Catalog;
using Synthia.Terminal.Core.Sales;
using Synthia.Terminal.Core.Sync;

namespace Synthia.Terminal.App.ViewModels;

public enum SyncLevel { Ok, Pending, Alert }

public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>Acima disso o gerente precisa olhar: o servidor não está recebendo.</summary>
    public const int PendingAlertThreshold = 200;

    private readonly TerminalHost _host;
    private readonly DispatcherTimer _timer;

    public MainWindowViewModel(TerminalHost host)
    {
        _host = host;
        TerminalId = host.Config.Terminal.Id;
        var store = host.Get<StoreProfileCache>();
        StoreBrand.Current.Attach(store);
        Banner = host.Demo ? "DEMONSTRAÇÃO · dispositivos simulados"
            : host.Config.Nfce.Ambiente == NfceEnvironment.Homologacao ? "HOMOLOGAÇÃO · NFC-e sem valor fiscal" : null;
        if (store.CnpjMismatch) Banner = "CNPJ do cadastro da loja difere do configurado neste caixa: confira o certificado.";
        if (host.ConfigErrors.Count > 0) Banner = "Configuração: " + string.Join(" · ", host.ConfigErrors);

        ShowCurrentScreen();
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick());
        _timer.Start();
        Tick();
    }

    public string TerminalId { get; }
    public string? Banner { get; }

    [ObservableProperty]
    public partial ObservableObject Screen { get; set; } = null!;

    [ObservableProperty]
    public partial string OperatorName { get; set; } = "";

    [ObservableProperty]
    public partial string Clock { get; set; } = "";

    [ObservableProperty]
    public partial string SyncText { get; set; } = "";

    [ObservableProperty]
    public partial SyncLevel SyncLevel { get; set; }

    private void ShowCurrentScreen()
    {
        var shifts = _host.Get<ShiftService>();
        if (shifts.CurrentShiftId() is null)
        {
            Screen = new OpenShiftViewModel(shifts, ShowCurrentScreen);
            OperatorName = "Caixa fechado";
        }
        else
        {
            Screen = new SaleViewModel(_host.Get<CheckoutSession>(), shifts, ShowCurrentScreen);
            OperatorName = shifts.CurrentOperator() ?? "";
        }
    }

    private void Tick()
    {
        Clock = DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss", Formatting.PtBr);

        var pending = _host.Get<OutboxPusher>().PendingCount();
        var version = _host.Get<LocalCatalog>().CurrentVersion();
        SyncLevel = pending == 0 ? SyncLevel.Ok : pending < PendingAlertThreshold ? SyncLevel.Pending : SyncLevel.Alert;
        SyncText = pending == 0 ? $"Sincronizado · catálogo v{version}" : $"{pending} evento(s) a enviar · catálogo v{version}";
    }
}
