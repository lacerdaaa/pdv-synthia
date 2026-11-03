using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Synthia.Terminal.App.ViewModels;
using Synthia.Terminal.Core.Sales;

namespace Synthia.Terminal.App;

/// <summary>
/// Cores de estado (tokens Synthia): sincronização e mensagens ao operador. Registros guardados no caixa usam o
/// azul de offline, não vermelho: o caixa está vendendo normalmente. Vermelho só para o que pede ação do operador.
/// </summary>
public sealed class StateBrushConverter : IValueConverter
{
    public static readonly StateBrushConverter Instance = new();

    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#1F7A3D"));
    private static readonly IBrush Offline = new SolidColorBrush(Color.Parse("#3B5B85"));
    private static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#A15C07"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#B42318"));
    private static readonly IBrush Neutral = new SolidColorBrush(Color.Parse("#0B5563"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        SyncLevel.Ok or FeedbackKind.Success => Ok,
        SyncLevel.Pending => Offline,
        SyncLevel.Alert or FeedbackKind.Warning => Warn,
        FeedbackKind.Error => Bad,
        _ => Neutral,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
