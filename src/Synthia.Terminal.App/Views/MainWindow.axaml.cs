using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Synthia.Terminal.App.ViewModels;

namespace Synthia.Terminal.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Túnel: os atalhos de PDV chegam antes da caixa de texto, que só recebe os caracteres.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel { Screen: SaleViewModel sale } && IsShortcut(e.Key))
        {
            e.Handled = true;
            await sale.HandleKeyAsync(e.Key);
        }
    }

    private static bool IsShortcut(Key key) =>
        key is Key.Enter or Key.Escape or Key.Delete or >= Key.F1 and <= Key.F12;
}
