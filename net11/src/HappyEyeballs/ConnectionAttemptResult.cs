using System.Net;
using System.Net.Sockets;

namespace HappyEyeballs;

/// <summary>
/// Represents the result of a Happy Eyeballs connection attempt.
/// </summary>
public sealed record ConnectionAttemptResult
{
    /// <summary>
    /// Gets a value indicating whether the connection was successful.
    /// </summary>
    public bool IsSuccessful { get; init; }

    /// <summary>
    /// Gets the connected socket if the connection was successful.
    /// </summary>
    public Socket? Socket { get; init; }

    /// <summary>
    /// Gets the IP address that was successfully connected to.
    /// </summary>
    public IPAddress? ConnectedAddress { get; init; }

    /// <summary>
    /// Gets the exception that occurred if the connection failed.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets all attempted IP addresses during the connection process.
    /// </summary>
    public IReadOnlyList<IPAddress> AttemptedAddresses { get; init; } = Array.Empty<IPAddress>();

    /// <summary>
    /// Gets the time elapsed for the connection attempt.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Creates a successful connection result.
    /// </summary>
    public static ConnectionAttemptResult Success(Socket socket, IPAddress address, IReadOnlyList<IPAddress> attemptedAddresses, TimeSpan elapsed)
    {
        return new ConnectionAttemptResult
        {
            IsSuccessful = true,
            Socket = socket,
            ConnectedAddress = address,
            AttemptedAddresses = attemptedAddresses,
            Elapsed = elapsed
        };
    }

    /// <summary>
    /// Creates a failed connection result.
    /// </summary>
    public static ConnectionAttemptResult Failure(Exception exception, IReadOnlyList<IPAddress> attemptedAddresses, TimeSpan elapsed)
    {
        return new ConnectionAttemptResult
        {
            IsSuccessful = false,
            Exception = exception,
            AttemptedAddresses = attemptedAddresses,
            Elapsed = elapsed
        };
    }
}