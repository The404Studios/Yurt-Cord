using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VeaMarketplace.Client.Converters;

/// <summary>
/// Converts bool to Visibility (true = Visible, false = Collapsed)
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Inverts a boolean value (true = false, false = true)
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>
/// Converts bool to Visibility inversely (true = Collapsed, false = Visible)
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Collapsed;
        }
        return true;
    }
}

/// <summary>
/// Converts DateTime to "time ago" string (e.g., "2 hours ago", "3 days ago")
/// </summary>
public class TimeAgoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Handle DateTime (nullable DateTime is unboxed to DateTime when it has a value)
        if (value is not DateTime dateTime)
        {
            return string.Empty;
        }

        var timeSpan = DateTime.Now - dateTime;

        if (timeSpan.TotalMinutes < 1)
            return "just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays}d ago";
        if (timeSpan.TotalDays < 30)
            return $"{(int)(timeSpan.TotalDays / 7)}w ago";
        if (timeSpan.TotalDays < 365)
            return $"{(int)(timeSpan.TotalDays / 30)}mo ago";
        return $"{(int)(timeSpan.TotalDays / 365)}y ago";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts null/empty string to Visibility (null/empty = Collapsed, has value = Visible)
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return Visibility.Collapsed;

        if (value is string str && string.IsNullOrWhiteSpace(str))
            return Visibility.Collapsed;

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts null/empty string to Visibility inversely (null/empty = Visible, has value = Collapsed)
/// </summary>
public class NullToVisibilityInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return Visibility.Visible;

        if (value is string str && string.IsNullOrWhiteSpace(str))
            return Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts string to Visibility
/// When parameter is null: checks if value is null/empty (null/empty = Collapsed, has value = Visible)
/// When parameter is provided: compares value to parameter (match = Visible, no match = Collapsed)
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    private readonly NullToVisibilityConverter _nullConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // If parameter is provided, do string comparison
        if (parameter is string targetValue)
        {
            if (value is string currentValue)
            {
                return currentValue == targetValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        // Otherwise, just check for null/empty
        return _nullConverter.Convert(value, targetType, parameter, culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts value equality to Visibility
/// Usage: Converter={StaticResource EqualityToVisibilityConverter}, ConverterParameter='ExpectedValue'
/// </summary>
public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var valueStr = value.ToString();
        var parameterStr = parameter.ToString();

        return string.Equals(valueStr, parameterStr, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts ban type bool to string (true = "Permanent Ban", false = "Temporary Ban")
/// </summary>
public class BanTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPermanent)
        {
            return isPermanent ? "Permanent Ban" : "Temporary Ban";
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts timestamp to formatted string
/// </summary>
public class TimestampConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Handle DateTime (nullable DateTime is unboxed to DateTime when it has a value)
        if (value is DateTime dateTime)
        {
            var format = parameter as string ?? "g"; // Default to general short date/time
            return dateTime.ToString(format, culture);
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Returns a default avatar URL if the provided URL is null or empty
/// </summary>
public class DefaultAvatarConverter : IValueConverter
{
    private const string DefaultAvatarUrl = "https://cdn.discordapp.com/embed/avatars/0.png";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string url && !string.IsNullOrWhiteSpace(url))
        {
            return url;
        }
        return parameter as string ?? DefaultAvatarUrl;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts zero count to Visibility (0 = Visible, >0 = Collapsed)
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts count to Visibility (0 = Collapsed, >0 = Visible)
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        if (value is System.Collections.ICollection collection)
        {
            return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts count to Visibility inversely (0 = Visible, >0 = Collapsed)
/// </summary>
public class CountToVisibilityInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        if (value is System.Collections.ICollection collection)
        {
            return collection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts positive number to Visibility (>0 = Visible, <=0 = Collapsed)
/// </summary>
public class PositiveToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal)
            return intVal > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is decimal decVal)
            return decVal > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is double dblVal)
            return dblVal > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts positive number to bool (>0 = true, <=0 = false)
/// </summary>
public class PositiveToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal)
            return intVal > 0;
        if (value is decimal decVal)
            return decVal > 0;
        if (value is double dblVal)
            return dblVal > 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts bool to text (true = "Yes"/"TrueText", false = "No"/"FalseText")
/// Use ConverterParameter to specify custom text: "TrueText|FalseText"
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;

        if (parameter is string paramStr && paramStr.Contains('|'))
        {
            var parts = paramStr.Split('|');
            return boolValue ? parts[0] : parts[1];
        }

        return boolValue ? "Yes" : "No";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts bool to color brush
/// Use ConverterParameter to specify colors: "TrueColor|FalseColor" (e.g., "#00FF00|#FF0000")
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;

        if (parameter is string paramStr && paramStr.Contains('|'))
        {
            var parts = paramStr.Split('|');
            var colorStr = boolValue ? parts[0] : parts[1];
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch
            {
                // Fall through to default
            }
        }

        // Default: green for true, red for false
        return boolValue
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 181, 129))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 71, 71));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts percentage (0-100 or 0-1) to width based on container
/// ConverterParameter specifies max width (default 100)
/// </summary>
public class PercentageToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double percentage = 0;
        double maxWidth = 100;

        if (value is double d)
            percentage = d > 1 ? d / 100 : d;
        else if (value is int i)
            percentage = i > 1 ? i / 100.0 : i;
        else if (value is decimal dec)
            percentage = (double)(dec > 1 ? dec / 100 : dec);

        if (parameter is double maxW)
            maxWidth = maxW;
        else if (parameter is string paramStr && double.TryParse(paramStr, out double parsed))
            maxWidth = parsed;

        return Math.Max(0, Math.Min(maxWidth, percentage * maxWidth));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Compares value to parameter and returns bool
/// </summary>
public class EqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null && parameter == null)
            return true;
        if (value == null || parameter == null)
            return false;

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts value greater than one to Visibility (>1 = Visible, <=1 = Collapsed)
/// </summary>
public class GreaterThanOneToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (value is System.Collections.ICollection collection)
            return collection.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
