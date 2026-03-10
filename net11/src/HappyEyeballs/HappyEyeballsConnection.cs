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
        IReadOnlyList<IPAddress> attemptedAddresses = Array.Empty<IPAddress>();
        _logger?.Invoke($"Starting connection to {host}:{port}");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Step 1: Resolve DNS addresses with both A and AAAA queries
            var addresses = await ResolveAddressesAsync(host, cancellationToken);
            
            if (addresses.Count == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            _logger?.Invoke($"Resolved {addresses.Count} addresses");

            // Step 2: Sort addresses according to RFC 6724 and RFC 8305
            var sortedAddresses = AddressSorter.Sort(addresses, _settings.PreferIPv6);
            attemptedAddresses = sortedAddresses;
            
            _logger?.Invoke($"Sorted addresses: {string.Join(", ", sortedAddresses.Select(a => a.ToString()))}");

            // Step 3: Delegate the connection race to the socket layer.
            var result = await AttemptConnectionsAsync(host, sortedAddresses, port, cancellationToken);
            
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger?.Invoke($"Connection to {host}:{port} was canceled");
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.Invoke($"Connection failed with exception: {ex.Message}");
            return ConnectionAttemptResult.Failure(ex, attemptedAddresses, stopwatch.Elapsed);
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
            _logger?.Invoke("Resolution delay elapsed, proceeding with available addresses");
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
    /// Attempts a connection using the .NET 11 socket-layer Happy Eyeballs implementation.
    /// </summary>
    private async Task<ConnectionAttemptResult> AttemptConnectionsAsync(
        string host,
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_settings.ConnectTimeout);

        try
        {
            EndPoint remoteEndPoint = IPAddress.TryParse(host, out var parsedAddress)
                ? new IPEndPoint(parsedAddress, port)
                : new DnsEndPoint(host, port);

            _logger?.Invoke($"Delegating connect race to socket layer with {ConnectAlgorithm.Parallel}");

            var socket = await ConnectWithParallelAlgorithmAsync(remoteEndPoint, timeoutCts.Token);
            socket.NoDelay = true;

            var connectedAddress = ((IPEndPoint)socket.RemoteEndPoint!).Address;
            return ConnectionAttemptResult.Success(socket, connectedAddress, addresses, TimeSpan.Zero);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out connecting to {host}:{port} after {_settings.ConnectTimeout}.");
        }
    }

    private Task<Socket> ConnectWithParallelAlgorithmAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken)
    {
        var socketEventArgs = new SocketAsyncEventArgs
        {
            RemoteEndPoint = remoteEndPoint
        };

        var completionSource = new TaskCompletionSource<Socket>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration cancellationRegistration = default;

        void Cleanup()
        {
            cancellationRegistration.Dispose();
            socketEventArgs.Completed -= OnCompleted;
            socketEventArgs.Dispose();
        }

        void CompleteFromCurrentState()
        {
            try
            {
                if (socketEventArgs.SocketError == SocketError.Success && socketEventArgs.ConnectSocket is not null)
                {
                    completionSource.TrySetResult(socketEventArgs.ConnectSocket);
                    return;
                }

                if (cancellationToken.IsCancellationRequested && socketEventArgs.SocketError is SocketError.OperationAborted or SocketError.ConnectionAborted or SocketError.Interrupted)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                    return;
                }

                completionSource.TrySetException(new SocketException((int)socketEventArgs.SocketError));
            }
            finally
            {
                Cleanup();
            }
        }

        void OnCompleted(object? _, SocketAsyncEventArgs __)
        {
            CompleteFromCurrentState();
        }

        socketEventArgs.Completed += OnCompleted;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(static state =>
            {
                var args = (SocketAsyncEventArgs)state!;

                try
                {
                    Socket.CancelConnectAsync(args);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }, socketEventArgs);
        }

        try
        {
            if (!Socket.ConnectAsync(SocketType.Stream, ProtocolType.Tcp, socketEventArgs, ConnectAlgorithm.Parallel))
            {
                CompleteFromCurrentState();
            }
        }
        catch (Exception ex)
        {
            Cleanup();
            completionSource.TrySetException(ex);
        }

        return completionSource.Task;
    }
}