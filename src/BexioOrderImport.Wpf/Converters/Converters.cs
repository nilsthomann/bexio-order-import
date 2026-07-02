using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BexioOrderImport.Wpf.Converters;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class IndexToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out int targetIndex))
        {
            return index == targetIndex;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr && int.TryParse(paramStr, out int targetIndex))
        {
            return targetIndex;
        }
        return Binding.DoNothing;
    }
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNullOrEmpty = value == null || (value is string s && string.IsNullOrEmpty(s));
        return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hexColor)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                return new SolidColorBrush(color);
            }
            catch
            {
                // Fallback to red
                return Brushes.Red;
            }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return false;
    }
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class EqualityToBooleanConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        var val1 = values[0];
        var val2 = values[1];
        if (val1 == null && val2 == null) return true;
        if (val1 == null || val2 == null) return false;
        return val1.Equals(val2);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
