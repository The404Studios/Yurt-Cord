using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Provides data validation utilities to prevent bugs and improve data quality
/// </summary>
public static class DataValidationHelper
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly Regex UsernameRegex = new(
        @"^[a-zA-Z0-9_-]{3,20}$",
        RegexOptions.Compiled
    );

    private static readonly Regex UrlRegex = new(
        @"^https?://[^\s/$.?#].[^\s]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Validates an email address
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return EmailRegex.IsMatch(email);
    }

    /// <summary>
    /// Validates a username (3-20 chars, alphanumeric, underscore, hyphen)
    /// </summary>
    public static bool IsValidUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        return UsernameRegex.IsMatch(username);
    }

    /// <summary>
    /// Validates a URL
    /// </summary>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return UrlRegex.IsMatch(url) && Uri.TryCreate(url, UriKind.Absolute, out _);
    }

    /// <summary>
    /// Validates a password meets minimum requirements
    /// </summary>
    public static bool IsValidPassword(string? password, int minLength = 8)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (password.Length < minLength)
            return false;

        // Must contain at least one letter and one number
        bool hasLetter = password.Any(char.IsLetter);
        bool hasDigit = password.Any(char.IsDigit);

        return hasLetter && hasDigit;
    }

    /// <summary>
    /// Gets password strength (0-100)
    /// </summary>
    public static int GetPasswordStrength(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return 0;

        int strength = 0;

        // Length bonus
        strength += Math.Min(password.Length * 4, 40);

        // Character variety bonuses
        if (password.Any(char.IsLower))
            strength += 10;

        if (password.Any(char.IsUpper))
            strength += 10;

        if (password.Any(char.IsDigit))
            strength += 10;

        if (password.Any(c => !char.IsLetterOrDigit(c)))
            strength += 15;

        // Penalty for repeated characters
        if (HasRepeatedChars(password))
            strength -= 10;

        // Penalty for sequential characters
        if (HasSequentialChars(password))
            strength -= 10;

        return Math.Max(0, Math.Min(100, strength));
    }

    /// <summary>
    /// Sanitizes user input to prevent XSS
    /// </summary>
    public static string SanitizeInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Remove potentially dangerous characters
        var sanitized = input
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;")
            .Replace("/", "&#x2F;");

        return sanitized;
    }

    /// <summary>
    /// Validates a file path is safe (no directory traversal)
    /// </summary>
    public static bool IsSafeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Check for directory traversal attempts
        if (path.Contains("..") || path.Contains("//") || path.Contains("\\\\"))
            return false;

        // Check for absolute paths
        if (path.StartsWith("/") || path.StartsWith("\\") || path.Contains(":"))
            return false;

        return true;
    }

    /// <summary>
    /// Validates a GUID string
    /// </summary>
    public static bool IsValidGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        return Guid.TryParse(guid, out _);
    }

    /// <summary>
    /// Validates a numeric range
    /// </summary>
    public static bool IsInRange(int value, int min, int max)
    {
        return value >= min && value <= max;
    }

    /// <summary>
    /// Validates a numeric range (double)
    /// </summary>
    public static bool IsInRange(double value, double min, double max)
    {
        return value >= min && value <= max;
    }

    /// <summary>
    /// Validates a string length
    /// </summary>
    public static bool IsValidLength(string? text, int minLength, int maxLength)
    {
        if (text == null)
            return minLength == 0;

        return text.Length >= minLength && text.Length <= maxLength;
    }

    /// <summary>
    /// Validates a collection is not null or empty
    /// </summary>
    public static bool IsNotNullOrEmpty<T>(IEnumerable<T>? collection)
    {
        return collection != null && collection.Any();
    }

    /// <summary>
    /// Validates an object is not null
    /// </summary>
    public static bool IsNotNull(object? obj)
    {
        return obj != null;
    }

    /// <summary>
    /// Validates multiple conditions and returns first error message
    /// </summary>
    public static (bool isValid, string? errorMessage) ValidateAll(params (bool condition, string errorMessage)[] validations)
    {
        foreach (var (condition, errorMessage) in validations)
        {
            if (!condition)
            {
                return (false, errorMessage);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Truncates a string to a maximum length
    /// </summary>
    public static string Truncate(string? text, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - suffix.Length) + suffix;
    }

    /// <summary>
    /// Validates a hex color code
    /// </summary>
    public static bool IsValidHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return false;

        var hexRegex = new Regex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$");
        return hexRegex.IsMatch(color);
    }

    /// <summary>
    /// Validates a version string (e.g., "1.2.3")
    /// </summary>
    public static bool IsValidVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        return Version.TryParse(version, out _);
    }

    /// <summary>
    /// Checks if a string contains profanity or inappropriate content
    /// </summary>
    public static bool ContainsProfanity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Basic profanity filter (should be expanded based on requirements)
        var profanityWords = Array.Empty<string>();

        var lowerText = text.ToLower();
        return profanityWords.Any(word => lowerText.Contains(word));
    }

    /// <summary>
    /// Checks for repeated characters
    /// </summary>
    private static bool HasRepeatedChars(string text)
    {
        for (int i = 0; i < text.Length - 2; i++)
        {
            if (text[i] == text[i + 1] && text[i] == text[i + 2])
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks for sequential characters (abc, 123)
    /// </summary>
    private static bool HasSequentialChars(string text)
    {
        for (int i = 0; i < text.Length - 2; i++)
        {
            if (text[i] + 1 == text[i + 1] && text[i] + 2 == text[i + 2])
                return true;
        }

        return false;
    }
}
