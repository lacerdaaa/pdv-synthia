using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Synthia.Terminal.App.Views;

public partial class SaleView : UserControl
{
    public SaleView()
    {
        InitializeComponent();
        // O leitor "digita" na caixa de entrada: ela precisa estar sempre com o foco.
        AddHandler(PointerReleasedEvent, (_, _) => InputBox.Focus(), RoutingStrategies.Bubble, handledEventsToo: true);
        ItemList.SelectionChanged += (_, _) => InputBox.Focus();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        InputBox.Focus();
    }
}
