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
        DateTime dateTime;

        // Handle both DateTime and DateTime?
        if (value is DateTime dt)
        {
            dateTime = dt;
        }
        else if (value is DateTime? nullableDt && nullableDt.HasValue)
        {
            dateTime = nullableDt.Value;
        }
        else
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
        return Binding.DoNothing;
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
        return Binding.DoNothing;
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
        return Binding.DoNothing;
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
        return Binding.DoNothing;
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
        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts timestamp to formatted string
/// </summary>
public class TimestampConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        DateTime dateTime;

        // Handle both DateTime and DateTime?
        if (value is DateTime dt)
        {
            dateTime = dt;
        }
        else if (value is DateTime? nullableDt && nullableDt.HasValue)
        {
            dateTime = nullableDt.Value;
        }
        else
        {
            return string.Empty;
        }

        var format = parameter as string ?? "g"; // Default to general short date/time
        return dateTime.ToString(format, culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
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
        return Binding.DoNothing;
    }
}
