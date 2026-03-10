using HappyEyeballs;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Happy Eyeballs v2 (RFC 8305) Sample Application");
Console.WriteLine("=================================================\n");

// Example 1: Connect to a well-known dual-stack service with default settings
await Example1_BasicConnection();

Console.WriteLine("\n" + new string('-', 80) + "\n");

// Example 2: Connect with custom settings and logging enabled
await Example2_CustomSettings();

Console.WriteLine("\n" + new string('-', 80) + "\n");

// Example 3: Connect to multiple hosts and compare
await Example3_MultipleHosts();

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

static async Task Example1_BasicConnection()
{
    Console.WriteLine("Example 1: Basic Connection");
    Console.WriteLine("---------------------------");
    
    var connection = new HappyEyeballsConnection();
    
    try
    {
        // Connect to Google's public DNS (supports both IPv4 and IPv6)
        var result = await connection.ConnectAsync("google.com", 80);
        
        if (result.IsSuccessful && result.Socket != null)
        {
            Console.WriteLine($"✓ Successfully connected!");
            Console.WriteLine($"  Connected to: {result.ConnectedAddress}");
            Console.WriteLine($"  Address family: {result.ConnectedAddress?.AddressFamily}");
            Console.WriteLine($"  Connection time: {result.Elapsed.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Attempted addresses: {result.AttemptedAddresses.Count}");
            
            // Send a simple HTTP request
            await SendHttpRequest(result.Socket, "google.com");
            
            result.Socket.Dispose();
        }
        else
        {
            Console.WriteLine($"✗ Connection failed: {result.Exception?.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Exception: {ex.Message}");
    }
}

static async Task Example2_CustomSettings()
{
    Console.WriteLine("Example 2: Connection with Custom Settings and Logging");
    Console.WriteLine("-------------------------------------------------------");
    
    var settings = new HappyEyeballsConnectionSettings
    {
        EnableLogging = true,
        ConnectionAttemptDelay = TimeSpan.FromMilliseconds(100), // Faster than default
        ResolutionDelay = TimeSpan.FromMilliseconds(25),
        PreferIPv6 = true,
        ConnectTimeout = TimeSpan.FromSeconds(5)
    };
    
    var connection = new HappyEyeballsConnection(settings);
    
    try
    {
        var result = await connection.ConnectAsync("www.microsoft.com", 443);
        
        if (result.IsSuccessful && result.Socket != null)
        {
            Console.WriteLine($"\n✓ Connected successfully!");
            Console.WriteLine($"  Connected to: {result.ConnectedAddress}");
            Console.WriteLine($"  Total time: {result.Elapsed.TotalMilliseconds:F2}ms");
            
            result.Socket.Dispose();
        }
        else
        {
            Console.WriteLine($"\n✗ Connection failed: {result.Exception?.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n✗ Exception: {ex.Message}");
    }
}

static async Task Example3_MultipleHosts()
{
    Console.WriteLine("Example 3: Compare Connections to Multiple Hosts");
    Console.WriteLine("-------------------------------------------------");
    
    var hosts = new[]
    {
        ("github.com", 443),
        ("cloudflare.com", 443),
        ("stackoverflow.com", 443)
    };
    
    var connection = new HappyEyeballsConnection(new HappyEyeballsConnectionSettings
    {
        EnableLogging = false
    });
    
    foreach (var (host, port) in hosts)
    {
        try
        {
            Console.WriteLine($"\nConnecting to {host}:{port}...");
            var result = await connection.ConnectAsync(host, port);
            
            if (result.IsSuccessful && result.Socket != null)
            {
                Console.WriteLine($"  ✓ Connected to {result.ConnectedAddress} ({result.ConnectedAddress?.AddressFamily})");
                Console.WriteLine($"  ⏱ Time: {result.Elapsed.TotalMilliseconds:F2}ms");
                Console.WriteLine($"  📊 Addresses tried: {result.AttemptedAddresses.Count}");
                Console.WriteLine($"  📋 Addresses: {string.Join(", ", result.AttemptedAddresses.Take(3))}");
                
                result.Socket.Dispose();
            }
            else
            {
                Console.WriteLine($"  ✗ Failed: {result.Exception?.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Exception: {ex.Message}");
        }
    }
}

static async Task SendHttpRequest(Socket socket, string host)
{
    try
    {
        // Send a simple HTTP GET request
        var request = $"GET / HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n\r\n";
        var requestBytes = Encoding.ASCII.GetBytes(request);
        
        await socket.SendAsync(requestBytes, SocketFlags.None);
        
        // Read the first part of the response
        var buffer = new byte[1024];
        var received = await socket.ReceiveAsync(buffer, SocketFlags.None);
        
        if (received > 0)
        {
            var response = Encoding.ASCII.GetString(buffer, 0, Math.Min(received, 200));
            var firstLine = response.Split('\r', '\n')[0];
            Console.WriteLine($"  HTTP Response: {firstLine}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ⚠ HTTP request failed: {ex.Message}");
    }
}