using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Synthia.Terminal.App.Views;

public partial class OpenShiftView : UserControl
{
    public OpenShiftView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Operator.Focus();
    }
}
