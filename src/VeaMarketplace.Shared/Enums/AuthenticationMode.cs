namespace VeaMarketplace.Shared.Enums;

/// <summary>
/// Defines the authentication mode used by the server.
/// </summary>
public enum AuthenticationMode
{
    /// <summary>
    /// Standard session-based authentication using JWT tokens.
    /// Users can log in from any device with valid credentials.
    /// </summary>
    Session = 0,

    /// <summary>
    /// Hardware ID (HWID) based authentication.
    /// Ties user accounts to specific hardware fingerprints.
    /// Prevents account sharing across multiple machines.
    /// </summary>
    Hwid = 1,

    /// <summary>
    /// Whitelist-based authentication.
    /// Only pre-approved users or IP addresses can register/login.
    /// Useful for private or invite-only servers.
    /// </summary>
    Whitelist = 2
}
