using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Synthia.Terminal.Core.Sync;

namespace Synthia.Terminal.App;

/// <summary>
/// Marca da loja na tela do caixa: nome e logo do cadastro sincronizado (ou a logo padrão do produto) e as cores,
/// aplicadas nos pincéis do tema. Os pincéis são alterados no lugar, então toda a tela muda junto, sem reabrir.
/// </summary>
public sealed partial class StoreBrand : ObservableObject
{
    private static readonly Bitmap DefaultLogo = new(AssetLoader.Open(new Uri("avares://synthia-caixa/Assets/logo-padrao.png")));

    public static StoreBrand Current { get; } = new();

    private StoreProfileCache? _cache;

    [ObservableProperty]
    public partial Bitmap Logo { get; set; } = DefaultLogo;

    [ObservableProperty]
    public partial string StoreName { get; set; } = "";

    /// <summary>Passa a seguir o cadastro deste caixa; cada atualização do sync chega na tela.</summary>
    public void Attach(StoreProfileCache cache)
    {
        if (_cache is not null) _cache.Changed -= OnChanged;
        _cache = cache;
        cache.Changed += OnChanged;
        Apply(cache);
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        if (sender is StoreProfileCache cache) Dispatcher.UIThread.Post(() => Apply(cache));
    }

    private void Apply(StoreProfileCache cache)
    {
        StoreName = cache.Receipt.Name;
        Logo = cache.HasLogo ? TryLoad(cache.LogoPath) ?? DefaultLogo : DefaultLogo;
        if (cache.Profile is { } p) ApplyColors(p.PrimaryColor, p.AccentColor);
    }

    private static Bitmap? TryLoad(string path)
    {
        try { return new Bitmap(new MemoryStream(File.ReadAllBytes(path))); }
        catch (Exception) { return null; } // arquivo corrompido: fica a logo padrão até o próximo sync
    }

    private static void ApplyColors(string primary, string accent)
    {
        if (Application.Current is not { } app) return;
        if (Color.TryParse(primary, out var navy))
        {
            SetBrush(app, "NavyBrush", navy);
            SetBrush(app, "NavyDarkBrush", Color.FromRgb((byte)(navy.R * 0.7), (byte)(navy.G * 0.7), (byte)(navy.B * 0.7)));
        }
        if (Color.TryParse(accent, out var red)) SetBrush(app, "RedBrush", red);
    }

    private static void SetBrush(Application app, string key, Color color)
    {
        if (app.Resources.TryGetResource(key, null, out var value) && value is SolidColorBrush brush) brush.Color = color;
    }
}
