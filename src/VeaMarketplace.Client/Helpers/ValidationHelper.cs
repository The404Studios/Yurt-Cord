using System.Text.RegularExpressions;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Provides consistent validation methods across the application.
/// Uses AppConstants for limits to ensure consistency.
/// </summary>
public static partial class ValidationHelper
{
    #region String Validation

    /// <summary>Validates that a string is not null or whitespace.</summary>
    public static ValidationResult ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error($"{fieldName} is required");
        return ValidationResult.Success();
    }

    /// <summary>Validates string length is within limits.</summary>
    public static ValidationResult ValidateLength(string? value, string fieldName, int minLength, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return ValidationResult.Error($"{fieldName} is required");

        if (value.Length < minLength)
            return ValidationResult.Error($"{fieldName} must be at least {minLength} characters");

        if (value.Length > maxLength)
            return ValidationResult.Error($"{fieldName} cannot exceed {maxLength} characters");

        return ValidationResult.Success();
    }

    #endregion

    #region Username Validation

    /// <summary>Validates a username against app requirements.</summary>
    public static ValidationResult ValidateUsername(string? username)
    {
        var required = ValidateRequired(username, "Username");
        if (!required.IsValid) return required;

        var length = ValidateLength(username, "Username",
            AppConstants.MinUsernameLength, AppConstants.MaxUsernameLength);
        if (!length.IsValid) return length;

        // Username should only contain alphanumeric, underscore, period
        if (!UsernameRegex().IsMatch(username!))
            return ValidationResult.Error("Username can only contain letters, numbers, underscores, and periods");

        return ValidationResult.Success();
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_\.]+$")]
    private static partial Regex UsernameRegex();

    #endregion

    #region Password Validation

    /// <summary>Validates a password against app requirements.</summary>
    public static ValidationResult ValidatePassword(string? password)
    {
        var required = ValidateRequired(password, "Password");
        if (!required.IsValid) return required;

        if (password!.Length < AppConstants.MinPasswordLength)
            return ValidationResult.Error($"Password must be at least {AppConstants.MinPasswordLength} characters");

        return ValidationResult.Success();
    }

    /// <summary>Validates that passwords match.</summary>
    public static ValidationResult ValidatePasswordMatch(string? password, string? confirmPassword)
    {
        if (password != confirmPassword)
            return ValidationResult.Error("Passwords do not match");
        return ValidationResult.Success();
    }

    #endregion

    #region Email Validation

    /// <summary>Validates an email address.</summary>
    public static ValidationResult ValidateEmail(string? email)
    {
        var required = ValidateRequired(email, "Email");
        if (!required.IsValid) return required;

        if (!EmailRegex().IsMatch(email!))
            return ValidationResult.Error("Please enter a valid email address");

        return ValidationResult.Success();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    #endregion

    #region Message Validation

    /// <summary>Validates a message.</summary>
    public static ValidationResult ValidateMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return ValidationResult.Error("Message cannot be empty");

        if (message.Length > AppConstants.MaxMessageLength)
            return ValidationResult.Error($"Message cannot exceed {AppConstants.MaxMessageLength} characters");

        return ValidationResult.Success();
    }

    #endregion

    #region Bio/Status Validation

    /// <summary>Validates a bio.</summary>
    public static ValidationResult ValidateBio(string? bio)
    {
        if (string.IsNullOrEmpty(bio))
            return ValidationResult.Success(); // Bio is optional

        if (bio.Length > AppConstants.MaxBioLength)
            return ValidationResult.Error($"Bio cannot exceed {AppConstants.MaxBioLength} characters");

        return ValidationResult.Success();
    }

    /// <summary>Validates a status message.</summary>
    public static ValidationResult ValidateStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return ValidationResult.Success(); // Status is optional

        if (status.Length > AppConstants.MaxStatusLength)
            return ValidationResult.Error($"Status cannot exceed {AppConstants.MaxStatusLength} characters");

        return ValidationResult.Success();
    }

    #endregion
}

/// <summary>Result of a validation operation.</summary>
public readonly struct ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Error(string message) => new(false, message);

    /// <summary>Implicit conversion to bool for easy if checks.</summary>
    public static implicit operator bool(ValidationResult result) => result.IsValid;
}
