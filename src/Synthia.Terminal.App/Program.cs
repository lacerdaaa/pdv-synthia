using Avalonia;

namespace Synthia.Terminal.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.Args = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
