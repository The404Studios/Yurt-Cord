using System;
using System.Globalization;
using System.Windows.Data;

namespace VeaMarketplace.Client.Converters;

public class AudioLevelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double audioLevel && values[1] is double parentWidth)
        {
            return Math.Max(0, Math.Min(parentWidth, audioLevel * parentWidth));
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return Array.Empty<object>();
    }
}

public class SliderWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double sliderValue)
        {
            return sliderValue * 100; // Assuming 100px max width
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

public class AudioLevelToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double level)
        {
            return level * 40; // Max height of 40
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// MultiValueConverter that compares two values for equality.
/// Returns true if the values are equal, false otherwise.
/// </summary>
public class EqualityMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return false;

        var value1 = values[0];
        var value2 = values[1];

        if (value1 == null && value2 == null)
            return true;

        if (value1 == null || value2 == null)
            return false;

        return value1.Equals(value2);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return Array.Empty<object>();
    }
}
