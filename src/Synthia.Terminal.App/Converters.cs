using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Synthia.Terminal.App.ViewModels;
using Synthia.Terminal.Core.Sales;

namespace Synthia.Terminal.App;

/// <summary>Cores de estado: sincronização e mensagens ao operador.</summary>
public sealed class StateBrushConverter : IValueConverter
{
    public static readonly StateBrushConverter Instance = new();

    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#1F8A4C"));
    private static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#B26B00"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#C2362B"));
    private static readonly IBrush Neutral = new SolidColorBrush(Color.Parse("#4A4741"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        SyncLevel.Ok or FeedbackKind.Success => Ok,
        SyncLevel.Pending or FeedbackKind.Warning => Warn,
        SyncLevel.Alert or FeedbackKind.Error => Bad,
        _ => Neutral,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
