using System.Globalization;
using System.Windows.Data;

namespace DynamicIslandBar;

/// <summary>
/// Converts a percentage value (0-100) to a scale factor (0-1) for ProgressBar templates.
/// </summary>
public class PercentToScaleConverter : IValueConverter
{
    public static readonly PercentToScaleConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return d / 100.0;
        if (value is int i)
            return i / 100.0;
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}