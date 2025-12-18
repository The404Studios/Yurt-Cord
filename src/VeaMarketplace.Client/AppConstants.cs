namespace VeaMarketplace.Client;

/// <summary>
/// Application-wide constants and configuration values.
/// Centralized to avoid magic numbers and enable easy configuration changes.
/// </summary>
public static class AppConstants
{
    #region Application Identity

    /// <summary>The application name displayed throughout the UI.</summary>
    public const string AppName = "Plugin";

    /// <summary>The application tagline.</summary>
    public const string AppTagline = "Connect. Trade. Create.";

    /// <summary>Current application version.</summary>
    public const string AppVersion = "1.0.0";

    /// <summary>URL scheme for deep linking.</summary>
    public const string UrlScheme = "plugin://";

    #endregion

    #region Server Configuration

    /// <summary>Default server URL for development.</summary>
    public const string DefaultServerUrl = "http://localhost:5000";

    /// <summary>API base path.</summary>
    public const string ApiBasePath = "/api";

    /// <summary>SignalR hub paths.</summary>
    public static class Hubs
    {
        public const string Chat = "/hubs/chat";
        public const string Voice = "/hubs/voice";
        public const string Profile = "/hubs/profile";
        public const string Friends = "/hubs/friends";
        public const string Content = "/hubs/content";
    }

    #endregion

    #region Timeouts and Intervals

    /// <summary>Default HTTP request timeout in seconds.</summary>
    public const int DefaultHttpTimeoutSeconds = 30;

    /// <summary>SignalR connection timeout in seconds.</summary>
    public const int SignalRTimeoutSeconds = 30;

    /// <summary>Voice activity detection threshold.</summary>
    public const double DefaultVoiceActivityThreshold = 0.02;

    /// <summary>Typing indicator duration in milliseconds.</summary>
    public const int TypingIndicatorDurationMs = 3000;

    /// <summary>Toast notification display duration in milliseconds.</summary>
    public const int ToastDurationMs = 4000;

    /// <summary>Thread join timeout in milliseconds.</summary>
    public const int ThreadJoinTimeoutMs = 1000;

    #endregion

    #region UI Limits

    /// <summary>Maximum message length.</summary>
    public const int MaxMessageLength = 2000;

    /// <summary>Maximum username length.</summary>
    public const int MaxUsernameLength = 32;

    /// <summary>Minimum username length.</summary>
    public const int MinUsernameLength = 3;

    /// <summary>Minimum password length.</summary>
    public const int MinPasswordLength = 8;

    /// <summary>Maximum bio length.</summary>
    public const int MaxBioLength = 500;

    /// <summary>Maximum status message length.</summary>
    public const int MaxStatusLength = 128;

    /// <summary>Maximum voice room participants.</summary>
    public const int MaxVoiceRoomParticipants = 50;

    /// <summary>Minimum voice room participants.</summary>
    public const int MinVoiceRoomParticipants = 2;

    #endregion

    #region Audio/Video

    /// <summary>Default volume level (0.0 to 1.0).</summary>
    public const double DefaultVolume = 1.0;

    /// <summary>Maximum user volume boost (200%).</summary>
    public const float MaxVolumeBoost = 2.0f;

    /// <summary>Default screen share FPS.</summary>
    public const int DefaultScreenShareFps = 30;

    /// <summary>Default screen share resolution width.</summary>
    public const int DefaultScreenShareWidth = 1920;

    /// <summary>Default screen share resolution height.</summary>
    public const int DefaultScreenShareHeight = 1080;

    #endregion

    #region Theme Colors (for code-behind usage)

    /// <summary>Primary accent color (Plugin Orange).</summary>
    public const string AccentPrimaryHex = "#FF6B00";

    /// <summary>Lighter accent color.</summary>
    public const string AccentLightHex = "#FF8533";

    /// <summary>Secondary accent color (Gold-Orange).</summary>
    public const string AccentSecondaryHex = "#FFB347";

    /// <summary>Success color (Green).</summary>
    public const string SuccessColorHex = "#57F287";

    /// <summary>Error color (Red).</summary>
    public const string ErrorColorHex = "#ED4245";

    /// <summary>Warning color (Gold).</summary>
    public const string WarningColorHex = "#FFB347";

    #endregion
}
