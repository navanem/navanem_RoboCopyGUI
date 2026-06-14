using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using RoboSync.Core.Logging;

namespace RoboSync.App.Converters;

/// <summary>Maps a <see cref="LogLevel"/> to a foreground brush for the log panel.</summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Info = new(Color.FromRgb(0xC9, 0xD1, 0xD9));
    private static readonly SolidColorBrush Warning = new(Color.FromRgb(0xE3, 0xB3, 0x41));
    private static readonly SolidColorBrush Error = new(Color.FromRgb(0xF8, 0x73, 0x6B));
    private static readonly SolidColorBrush Raw = new(Color.FromRgb(0x8B, 0x94, 0x9E));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        LogLevel.Warning => Warning,
        LogLevel.Error => Error,
        LogLevel.Raw => Raw,
        _ => Info,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
