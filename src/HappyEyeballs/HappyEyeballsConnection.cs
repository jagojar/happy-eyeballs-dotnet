using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace HappyEyeballs;

/// <summary>
/// Implements Happy Eyeballs v2 (RFC 8305) connection algorithm for fast dual-stack connections.
/// </summary>
public sealed class HappyEyeballsConnection
{
    private readonly HappyEyeballsConnectionSettings _settings;
    private readonly Action<string>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HappyEyeballsConnection"/> class.
    /// </summary>
    /// <param name="settings">Configuration settings for the connection attempts.</param>
    public HappyEyeballsConnection(HappyEyeballsConnectionSettings? settings = null)
    {
        _settings = settings ?? new HappyEyeballsConnectionSettings();
        
        if (_settings.EnableLogging)
        {
            _logger = message => Console.WriteLine($"[HappyEyeballs] {message}");
        }
    }

    /// <summary>
    /// Connects to the specified host and port using Happy Eyeballs v2 algorithm.
    /// </summary>
    /// <param name="host">The hostname or IP address to connect to.</param>
    /// <param name="port">The port number to connect to.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the connection attempt.</returns>
    public async Task<ConnectionAttemptResult> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger?.Invoke($"Starting connection to {host}:{port}");

        try
        {
            // Step 1: Resolve DNS addresses with both A and AAAA queries
            var addresses = await ResolveAddressesAsync(host, cancellationToken);
            
            if (addresses.Count == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            _logger?.Invoke($"Resolved {addresses.Count} addresses");

            // Step 2: Sort addresses according to RFC 6724 and RFC 8305
            var sortedAddresses = AddressSorter.Sort(addresses, _settings.PreferIPv6);
            
            _logger?.Invoke($"Sorted addresses: {string.Join(", ", sortedAddresses.Select(a => a.ToString()))}");

            // Step 3: Attempt connections with staggered timing
            var result = await AttemptConnectionsAsync(sortedAddresses, port, cancellationToken);
            
            stopwatch.Stop();
            
            if (result.IsSuccessful)
            {
                _logger?.Invoke($"Successfully connected to {result.ConnectedAddress} in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
            }
            else
            {
                _logger?.Invoke($"All connection attempts failed after {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
            }

            return result with { Elapsed = stopwatch.Elapsed };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.Invoke($"Connection failed with exception: {ex.Message}");
            return ConnectionAttemptResult.Failure(ex, Array.Empty<IPAddress>(), stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Resolves DNS addresses for the given host with Resolution Delay (RFC 8305 Section 3).
    /// </summary>
    private async Task<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
        string host,
        CancellationToken cancellationToken)
    {
        // If already an IP address, return it directly
        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return new[] { ipAddress };
        }

        // RFC 8305 Section 3: Resolution Delay
        // Start both A and AAAA queries, but wait for the first response before waiting for the second
        _logger?.Invoke("Starting parallel DNS resolution for both A and AAAA records");

        var ipv6Task = ResolveAddressFamilyAsync(host, AddressFamily.InterNetworkV6, cancellationToken);
        var ipv4Task = ResolveAddressFamilyAsync(host, AddressFamily.InterNetwork, cancellationToken);

        // Wait for the first query to complete
        var firstCompleted = await Task.WhenAny(ipv6Task, ipv4Task);
        
        // Wait for Resolution Delay before proceeding with just the first result
        var delayTask = Task.Delay(_settings.ResolutionDelay, cancellationToken);
        var secondOrDelay = await Task.WhenAny(
            ipv6Task == firstCompleted ? ipv4Task : ipv6Task,
            delayTask
        );

        // Wait a bit more to see if we get both results
        if (secondOrDelay == delayTask)
        {
            _logger?.Invoke($"Resolution delay elapsed, proceeding with available addresses");
        }

        // Collect all resolved addresses
        var addresses = new List<IPAddress>();
        
        if (ipv6Task.IsCompletedSuccessfully)
        {
            addresses.AddRange(await ipv6Task);
        }
        
        if (ipv4Task.IsCompletedSuccessfully)
        {
            addresses.AddRange(await ipv4Task);
        }

        return addresses;
    }

    private async Task<IPAddress[]> ResolveAddressFamilyAsync(
        string host,
        AddressFamily addressFamily,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(host, addressFamily, cancellationToken);
            var addresses = entry.AddressList
                .Where(a => a.AddressFamily == addressFamily)
                .ToArray();
            
            _logger?.Invoke($"Resolved {addresses.Length} {addressFamily} addresses");
            return addresses;
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"Failed to resolve {addressFamily} addresses: {ex.Message}");
            return Array.Empty<IPAddress>();
        }
    }

    /// <summary>
    /// Attempts connections to multiple addresses with Connection Attempt Delay (RFC 8305 Section 5).
    /// </summary>
    private async Task<ConnectionAttemptResult> AttemptConnectionsAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var attemptedAddresses = new List<IPAddress>();
        var tasks = new List<Task<(Socket? Socket, IPAddress Address, Exception? Exception)>>();
        
        // RFC 8305 Section 5: Connection Attempt Delay
        // Start connection attempts with staggered timing
        for (int i = 0; i < addresses.Count; i++)
        {
            var address = addresses[i];
            attemptedAddresses.Add(address);
            
            _logger?.Invoke($"Starting connection attempt {i + 1} to {address}");
            
            var attemptTask = AttemptSingleConnectionAsync(address, port, cts.Token);
            tasks.Add(attemptTask);

            // For subsequent attempts, wait for Connection Attempt Delay
            if (i < addresses.Count - 1)
            {
                try
                {
                    // Wait for either:
                    // 1. Connection Attempt Delay to elapse, or
                    // 2. Any task to complete (success or failure)
                    var completedTask = await Task.WhenAny(
                        Task.Delay(_settings.ConnectionAttemptDelay, cts.Token),
                        Task.WhenAny(tasks)
                    );

                    // Check if any connection succeeded
                    var completedAttempts = tasks.Where(t => t.IsCompleted).ToList();
                    foreach (var attempt in completedAttempts)
                    {
                        var (socket, addr, exception) = await attempt;
                        if (socket != null)
                        {
                            _logger?.Invoke($"Connection to {addr} succeeded, cancelling other attempts");
                            await cts.CancelAsync();
                            
                            // Clean up other sockets
                            await CleanupFailedAttemptsAsync(tasks, socket);
                            
                            return ConnectionAttemptResult.Success(socket, addr, attemptedAddresses, TimeSpan.Zero);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // Wait for any connection to succeed or all to fail
        while (tasks.Count > 0)
        {
            try
            {
                var completedTask = await Task.WhenAny(tasks);
                var (socket, address, exception) = await completedTask;
                tasks.Remove(completedTask);

                if (socket != null)
                {
                    _logger?.Invoke($"Connection to {address} succeeded");
                    await cts.CancelAsync();
                    
                    // Clean up other sockets
                    await CleanupFailedAttemptsAsync(tasks, socket);
                    
                    return ConnectionAttemptResult.Success(socket, address, attemptedAddresses, TimeSpan.Zero);
                }
                else
                {
                    _logger?.Invoke($"Connection to {address} failed: {exception?.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // All attempts failed
        return ConnectionAttemptResult.Failure(
            new SocketException((int)SocketError.ConnectionRefused),
            attemptedAddresses,
            TimeSpan.Zero
        );
    }

    private async Task<(Socket? Socket, IPAddress Address, Exception? Exception)> AttemptSingleConnectionAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken)
    {
        Socket? socket = null;
        try
        {
            socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            // Apply socket options
            socket.NoDelay = true; // Disable Nagle's algorithm for lower latency
            
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_settings.ConnectTimeout);
            
            await socket.ConnectAsync(address, port, timeoutCts.Token);
            
            return (socket, address, null);
        }
        catch (Exception ex)
        {
            socket?.Dispose();
            return (null, address, ex);
        }
    }

    private async Task CleanupFailedAttemptsAsync(
        List<Task<(Socket? Socket, IPAddress Address, Exception? Exception)>> tasks,
        Socket successfulSocket)
    {
        foreach (var task in tasks)
        {
            try
            {
                if (task.IsCompleted)
                {
                    var (socket, _, _) = await task;
                    if (socket != null && socket != successfulSocket)
                    {
                        socket.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
