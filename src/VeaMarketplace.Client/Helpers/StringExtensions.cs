namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Extension methods for string manipulation.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Truncates a string to the specified length, adding ellipsis if truncated.
    /// </summary>
    public static string Truncate(this string? value, int maxLength, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        if (maxLength <= ellipsis.Length) return ellipsis[..maxLength];

        return value[..(maxLength - ellipsis.Length)] + ellipsis;
    }

    /// <summary>
    /// Formats a number for display (e.g., 1000 -> "1K", 1500000 -> "1.5M").
    /// </summary>
    public static string FormatNumber(this int number)
    {
        return number switch
        {
            >= 1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString()
        };
    }

    /// <summary>
    /// Formats a number for display (e.g., 1000 -> "1K", 1500000 -> "1.5M").
    /// </summary>
    public static string FormatNumber(this long number)
    {
        return number switch
        {
            >= 1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString()
        };
    }

    /// <summary>
    /// Formats a decimal as currency.
    /// </summary>
    public static string FormatCurrency(this decimal amount, string symbol = "$")
    {
        return $"{symbol}{amount:N2}";
    }

    /// <summary>
    /// Returns a relative time string (e.g., "2 hours ago", "just now").
    /// </summary>
    public static string ToRelativeTime(this DateTime dateTime)
    {
        var timeSpan = DateTime.Now - dateTime;

        if (timeSpan.TotalSeconds < 60)
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

    /// <summary>
    /// Returns a relative time string for nullable DateTime.
    /// </summary>
    public static string ToRelativeTime(this DateTime? dateTime, string defaultValue = "Never")
    {
        return dateTime?.ToRelativeTime() ?? defaultValue;
    }

    /// <summary>
    /// Checks if a string is a valid email format.
    /// </summary>
    public static bool IsValidEmail(this string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes HTML tags from a string.
    /// </summary>
    public static string StripHtml(this string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", string.Empty);
    }

    /// <summary>
    /// Converts the first character to uppercase.
    /// </summary>
    public static string Capitalize(this string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length == 1) return value.ToUpperInvariant();

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    /// <summary>
    /// Gets initials from a name (e.g., "John Doe" -> "JD").
    /// </summary>
    public static string GetInitials(this string? name, int maxLength = 2)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Concat(words.Take(maxLength).Select(w => char.ToUpperInvariant(w[0])));

        return string.IsNullOrEmpty(initials) ? "?" : initials;
    }
}
