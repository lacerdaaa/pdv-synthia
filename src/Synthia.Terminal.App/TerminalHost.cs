using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Synthia.Terminal.Core;
using Synthia.Terminal.Core.Catalog;
using Synthia.Terminal.Core.Devices;
using Synthia.Terminal.Core.Fiscal;
using Synthia.Terminal.Core.Sales;
using Synthia.Terminal.Core.Storage;
using Synthia.Terminal.Core.Sync;
using Synthia.Terminal.Devices;

namespace Synthia.Terminal.App;

/// <summary>
/// Composição do caixa: configuração, banco local, workers de sync, dispositivos e sessão de venda.
/// <c>--config caminho</c> escolhe o terminal.json; <c>--demo</c> usa dispositivos simulados e um catálogo de exemplo.
/// </summary>
public sealed class TerminalHost : IAsyncDisposable
{
    private readonly IHost _host;

    public TerminalConfig Config { get; }
    public IReadOnlyList<string> ConfigErrors { get; }
    public bool Demo { get; }

    private TerminalHost(IHost host, TerminalConfig config, IReadOnlyList<string> errors, bool demo)
    {
        _host = host;
        Config = config;
        ConfigErrors = errors;
        Demo = demo;
    }

    public T Get<T>() where T : notnull => _host.Services.GetRequiredService<T>();

    public static TerminalHost Build(string[] args, TerminalConfig? overrideConfig = null)
    {
        var demo = args.Contains("--demo");
        var config = overrideConfig ?? LoadConfig(args, demo);
        var errors = config.Validate();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();

        var s = builder.Services;
        s.AddSingleton(config);
        s.AddSingleton(config.ToOptions());
        s.AddSingleton(TimeProvider.System);
        s.AddSingleton(sp => { var db = new LocalDb(sp.GetRequiredService<TerminalOptions>()); db.EnsureCreated(); return db; });
        s.AddSingleton<Outbox>();
        s.AddSingleton<LocalCatalog>();
        s.AddSingleton(sp =>
        {
            var http = new HttpClient { BaseAddress = sp.GetRequiredService<TerminalOptions>().ServerUrl, Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("X-Terminal-Id", config.Terminal.Id);
            if (config.Terminal.ResolveApiKey() is { } key) http.DefaultRequestHeaders.Add("X-Terminal-Key", key);
            return new SyncClient(http);
        });
        s.AddSingleton<OutboxPusher>();
        s.AddSingleton<IOutboxSignal>(sp => sp.GetRequiredService<OutboxPusher>());
        s.AddHostedService(sp => sp.GetRequiredService<OutboxPusher>());
        s.AddSingleton<CatalogPuller>();
        s.AddHostedService(sp => sp.GetRequiredService<CatalogPuller>());
        s.AddSingleton<ShiftService>();
        s.AddSingleton(sp => new NfceNumbering(sp.GetRequiredService<LocalDb>(), config.Nfce.Serie));
        s.AddSingleton<INfceEmitter>(sp => new SimulatedNfceEmitter(sp.GetRequiredService<NfceNumbering>(), config.Store.Cnpj, TimeProvider.System));
        s.AddSingleton<SaleService>();
        s.AddSingleton(sp => new DeviceFactory(config.Devices, sp.GetRequiredService<ILoggerFactory>()));
        s.AddSingleton(sp => sp.GetRequiredService<DeviceFactory>().CreatePrinter());
        s.AddSingleton(sp => sp.GetRequiredService<DeviceFactory>().CreatePaymentTerminal());
        s.AddSingleton(sp => new ScanResolver(sp.GetRequiredService<LocalCatalog>(), config.ToScaleBarcodeFormat()));
        s.AddSingleton(new ReceiptBuilder(config.Store, config.Terminal.Id, config.Nfce.Ambiente));
        s.AddSingleton(sp => new CheckoutSession(
            sp.GetRequiredService<LocalCatalog>(), sp.GetRequiredService<ScanResolver>(), sp.GetRequiredService<SaleService>(),
            sp.GetRequiredService<ShiftService>(), sp.GetRequiredService<IReceiptPrinter>(), sp.GetRequiredService<IPaymentTerminal>(),
            sp.GetRequiredService<ReceiptBuilder>(), TimeProvider.System, sp.GetRequiredService<DeviceFactory>().CreateScale()));

        var host = new TerminalHost(builder.Build(), config, errors, demo);
        if (demo) DemoCatalog.SeedIfEmpty(host.Get<LocalCatalog>());
        return host;
    }

    /// <summary>Sobe os workers de sync. Servidor fora do ar não impede o caixa de abrir.</summary>
    public Task StartAsync() => _host.StartAsync();

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(TimeSpan.FromSeconds(3));
        _host.Dispose();
    }

    private static TerminalConfig LoadConfig(string[] args, bool demo)
    {
        var index = Array.IndexOf(args, "--config");
        var path = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(AppContext.BaseDirectory, "terminal.json");

        if (demo || !File.Exists(path))
        {
            return new TerminalConfig
            {
                Terminal = new TerminalSection { Id = "CX01", ServerUrl = "http://localhost:5290", DatabasePath = Path.Combine(AppContext.BaseDirectory, "caixa-demo.db") },
                Devices = new DevicesSection
                {
                    Scanner = new ScannerSection { Mode = ScannerMode.Keyboard },
                    Scale = new ScaleSection { Model = ScaleModel.Simulated },
                    Printer = new PrinterSection { Connection = PrinterConnection.Simulated },
                },
            };
        }

        return TerminalConfig.Load(path);
    }
}
