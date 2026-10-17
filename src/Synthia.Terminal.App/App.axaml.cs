using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Synthia.Terminal.App.ViewModels;
using Synthia.Terminal.App.Views;

namespace Synthia.Terminal.App;

public sealed class App : Application
{
    internal static string[] Args { get; set; } = [];

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var host = TerminalHost.Build(Args);
            _ = host.StartAsync();
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel(host) };
            desktop.ShutdownRequested += (_, _) => host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
