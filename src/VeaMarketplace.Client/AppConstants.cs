namespace VeaMarketplace.Client;

/// <summary>
/// Application-wide constants and configuration values.
/// Centralized to avoid magic numbers and enable easy configuration changes.
/// </summary>
public static class AppConstants
{
    #region Application Identity

    /// <summary>The application name displayed throughout the UI.</summary>
    public const string AppName = "Yurt Cord";

    /// <summary>The application tagline.</summary>
    public const string AppTagline = "Your Community, Your Way.";

    /// <summary>Current application version.</summary>
    public const string AppVersion = "1.0.0";

    /// <summary>URL scheme for deep linking.</summary>
    public const string UrlScheme = "yurtcord://";

    #endregion

    #region Server Configuration

    /// <summary>Default server URL for production.</summary>
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
        public const string Notifications = "/hubs/notifications";
        public const string Rooms = "/hubs/rooms";

        // Helper methods to get full URLs
        public static string GetChatUrl() => $"{DefaultServerUrl}{Chat}";
        public static string GetVoiceUrl() => $"{DefaultServerUrl}{Voice}";
        public static string GetProfileUrl() => $"{DefaultServerUrl}{Profile}";
        public static string GetFriendsUrl() => $"{DefaultServerUrl}{Friends}";
        public static string GetContentUrl() => $"{DefaultServerUrl}{Content}";
        public static string GetNotificationsUrl() => $"{DefaultServerUrl}{Notifications}";
        public static string GetRoomsUrl() => $"{DefaultServerUrl}{Rooms}";
    }

    /// <summary>API endpoint helpers.</summary>
    public static class Api
    {
        public static string GetBaseUrl() => $"{DefaultServerUrl}{ApiBasePath}";
        public static string GetFilesUrl() => $"{DefaultServerUrl}{ApiBasePath}/files";
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

    /// <summary>Primary accent color (Yurt Cord Teal).</summary>
    public const string AccentPrimaryHex = "#00B4D8";

    /// <summary>Lighter accent color (Yurt Cord Teal Light).</summary>
    public const string AccentLightHex = "#48CAE4";

    /// <summary>Darker accent color (Yurt Cord Teal Dark).</summary>
    public const string AccentDarkHex = "#0096C7";

    /// <summary>Secondary accent color (Gold).</summary>
    public const string AccentSecondaryHex = "#FFB347";

    /// <summary>Success color (Green).</summary>
    public const string SuccessColorHex = "#57F287";

    /// <summary>Error color (Red).</summary>
    public const string ErrorColorHex = "#ED4245";

    /// <summary>Warning color (Gold).</summary>
    public const string WarningColorHex = "#FFB347";

    #endregion

    #region Default Assets

    /// <summary>Default avatar path for users without custom avatars.</summary>
    public const string DefaultAvatarPath = "pack://application:,,,/Assets/default-avatar.png";

    /// <summary>Default banner path for users without custom banners.</summary>
    public const string DefaultBannerPath = "pack://application:,,,/Assets/default-banner.png";

    /// <summary>App logo path.</summary>
    public const string AppLogoPath = "pack://application:,,,/Assets/logo.png";

    #endregion

    #region Audio Configuration

    /// <summary>Audio sample rate in Hz.</summary>
    public const int AudioSampleRate = 48000;

    /// <summary>Opus frame size (20ms at 48kHz).</summary>
    public const int OpusFrameSize = 960;

    /// <summary>Maximum Opus frame buffer size.</summary>
    public const int MaxOpusFrameSize = 4000;

    /// <summary>Audio channels (mono).</summary>
    public const int AudioChannels = 1;

    #endregion

    #region WebRTC Configuration

    /// <summary>
    /// WebRTC ICE server configuration for NAT traversal.
    /// Uses public STUN servers for ICE candidate gathering.
    /// </summary>
    public static class WebRTC
    {
        /// <summary>Google's public STUN server.</summary>
        public const string GoogleStunServer = "stun:stun.l.google.com:19302";

        /// <summary>Backup STUN servers for redundancy.</summary>
        public static readonly string[] StunServers =
        [
            "stun:stun.l.google.com:19302",
            "stun:stun1.l.google.com:19302",
            "stun:stun2.l.google.com:19302",
            "stun:stun3.l.google.com:19302",
            "stun:stun4.l.google.com:19302"
        ];

        /// <summary>ICE gathering timeout in milliseconds.</summary>
        public const int IceGatheringTimeoutMs = 10000;

        /// <summary>P2P connection timeout in milliseconds.</summary>
        public const int P2PConnectionTimeoutMs = 15000;

        /// <summary>
        /// Whether to prefer P2P connections over server relay.
        /// When true, will attempt P2P first and fall back to server relay on failure.
        /// </summary>
        public const bool PreferP2PConnection = true;

        /// <summary>Maximum peers for P2P mesh network (for small group calls).</summary>
        public const int MaxP2PPeers = 4;
    }

    #endregion
}
