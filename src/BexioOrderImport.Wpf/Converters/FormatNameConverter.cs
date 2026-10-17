using System.Globalization;
using System.Windows.Data;
using BexioOrderImport.Abstractions.Formats;

namespace BexioOrderImport.Wpf.Converters;

/// <summary>
/// Converts a format id and the registered formats into the display name of the format.
/// </summary>
public class FormatNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        string? formatId = values.Length > 0 ? values[0] as string : null;
        var formats = values.Length > 1 ? values[1] as IEnumerable<IFormatAdapter> : null;

        var format = formats?.FirstOrDefault(f => string.Equals(f.FormatId, formatId, StringComparison.OrdinalIgnoreCase));
        return format?.DisplayName ?? string.Format(Resources.Translations.Settings_FormatUnavailable, formatId);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
