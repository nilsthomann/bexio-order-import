using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace BexioOrderImport.Wpf.Converters;

public class HtmlToNewLineConverter : IValueConverter
{
    private static readonly Regex BrRegex = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string input && !string.IsNullOrEmpty(input))
        {
            return BrRegex.Replace(input, Environment.NewLine);
        }
        return value ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
