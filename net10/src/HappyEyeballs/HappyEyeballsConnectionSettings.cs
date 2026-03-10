namespace HappyEyeballs;

/// <summary>
/// Configuration settings for Happy Eyeballs v2 (RFC 8305) connection attempts.
/// </summary>
public sealed class HappyEyeballsConnectionSettings
{
    /// <summary>
    /// Default delay between connection attempts (250ms as per RFC 8305).
    /// </summary>
    public static readonly TimeSpan DefaultConnectionAttemptDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Default delay to wait for both A and AAAA DNS records (50ms as per RFC 8305).
    /// </summary>
    public static readonly TimeSpan DefaultResolutionDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets the delay between initiating connection attempts.
    /// Default is 250ms as specified in RFC 8305.
    /// </summary>
    public TimeSpan ConnectionAttemptDelay { get; set; } = DefaultConnectionAttemptDelay;

    /// <summary>
    /// Gets or sets the delay to wait for both A and AAAA DNS records.
    /// Default is 50ms as specified in RFC 8305.
    /// </summary>
    public TimeSpan ResolutionDelay { get; set; } = DefaultResolutionDelay;

    /// <summary>
    /// Gets or sets whether to prefer IPv6 over IPv4 when both are available.
    /// Default is true as per RFC 8305 and RFC 6724.
    /// </summary>
    public bool PreferIPv6 { get; set; } = true;

    /// <summary>
    /// Gets or sets the connect timeout for individual connection attempts.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to enable detailed logging for debugging.
    /// </summary>
    public bool EnableLogging { get; set; } = false;
}
