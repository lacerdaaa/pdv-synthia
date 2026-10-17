using Avalonia;
using Avalonia.Headless;
using Synthia.Terminal.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Synthia.Terminal.App.Tests;

public static class TestAppBuilder
{
    // Renderização real (Skia) para poder capturar a tela nos testes.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
