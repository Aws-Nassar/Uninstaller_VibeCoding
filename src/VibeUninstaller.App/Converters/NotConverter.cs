using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VibeUninstaller.App.Converters;

/// <summary>Inverts a boolean. Registered as a singleton for XAML use.</summary>
public sealed class NotConverter : IValueConverter
{
    public static NotConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : true;
}

/// <summary>Shows an element when the bound boolean is true.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static BoolToVisibilityConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
