using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Sales;

namespace Synthia.Terminal.App.ViewModels;

public sealed partial class OpenShiftViewModel(ShiftService shifts, Action onOpened) : ObservableObject
{
    [ObservableProperty]
    public partial string OperatorName { get; set; } = "";

    [ObservableProperty]
    public partial string OpeningCash { get; set; } = "100,00";

    [ObservableProperty]
    public partial string? Error { get; set; }

    [RelayCommand]
    private void Open()
    {
        if (string.IsNullOrWhiteSpace(OperatorName)) { Error = "Informe o operador."; return; }
        if (!Formatting.TryParseMoney(OpeningCash, out var cents)) { Error = "Fundo de troco inválido."; return; }

        try
        {
            shifts.Open(OperatorName.Trim(), cents);
            Error = null;
            onOpened();
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
        }
    }
}
