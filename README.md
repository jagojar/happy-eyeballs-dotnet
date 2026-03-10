# Happy Eyeballs v2 for .NET 10

A complete implementation of **Happy Eyeballs Version 2** ([RFC 8305](https://tools.ietf.org/html/rfc8305)) for .NET 10, providing fast and reliable dual-stack (IPv4/IPv6) TCP connections.

## What is Happy Eyeballs v2?

Happy Eyeballs v2 (RFC 8305) is an algorithm designed to improve the user experience when connecting to services that support both IPv4 and IPv6. Instead of waiting for one protocol to fail before trying another, it:

- **Resolves both IPv4 and IPv6 addresses concurrently** with smart timing
- **Attempts connections in parallel** with staggered delays
- **Returns the first successful connection** immediately
- **Handles network conditions gracefully** without long timeouts

### Key Improvements over Happy Eyeballs v1

RFC 8305 improves on the original (RFC 6555) by:

1. **DNS Resolution Ordering**: Intelligent handling of both A and AAAA record queries
2. **Multiple Addresses per Family**: Better support for services with multiple IPs
3. **Deterministic Behavior**: More predictable connection patterns
4. **Clear Concurrency Rules**: Well-defined timing for connection attempts

## Features

✅ Full RFC 8305 compliance  
✅ Asynchronous/await support with cancellation tokens  
✅ Configurable delays and timeouts  
✅ Address sorting per RFC 6724 (Destination Address Selection)  
✅ Built-in logging for debugging  
✅ No external dependencies  
✅ .NET 10 compatible  

## Installation

Add the HappyEyeballs library to your project:

```bash
dotnet add reference path/to/HappyEyeballs.csproj
```

Or copy the source files into your project.

## Quick Start

### Basic Usage

```csharp
using HappyEyeballs;

var connection = new HappyEyeballsConnection();
var result = await connection.ConnectAsync("example.com", 443);

if (result.IsSuccessful && result.Socket != null)
{
    Console.WriteLine($"Connected to {result.ConnectedAddress}");
    // Use the socket...
    result.Socket.Dispose();
}
```

### With Custom Settings

```csharp
var settings = new HappyEyeballsConnectionSettings
{
    ConnectionAttemptDelay = TimeSpan.FromMilliseconds(250),
    ResolutionDelay = TimeSpan.FromMilliseconds(50),
    PreferIPv6 = true,
    ConnectTimeout = TimeSpan.FromSeconds(30),
    EnableLogging = true
};

var connection = new HappyEyeballsConnection(settings);
var result = await connection.ConnectAsync("example.com", 443);
```

### With Cancellation

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

var connection = new HappyEyeballsConnection();
var result = await connection.ConnectAsync("example.com", 443, cts.Token);
```

## Configuration Options

### HappyEyeballsConnectionSettings

| Property | Default | Description |
|----------|---------|-------------|
| `ConnectionAttemptDelay` | 250ms | Delay between starting connection attempts (RFC 8305) |
| `ResolutionDelay` | 50ms | Delay to wait for both A and AAAA DNS records |
| `PreferIPv6` | true | Whether to prefer IPv6 over IPv4 |
| `ConnectTimeout` | 30s | Timeout for individual connection attempts |
| `EnableLogging` | false | Enable detailed logging to console |

## How It Works

### 1. DNS Resolution (RFC 8305 Section 3)

```
Start both A and AAAA queries in parallel
    ↓
Wait for first response
    ↓
Wait ResolutionDelay (50ms) for second response
    ↓
Proceed with available addresses
```

### 2. Address Sorting (RFC 6724)

Addresses are sorted according to:
- **Address family preference** (IPv6 first by default)
- **Scope** (global > site-local > link-local)
- **Privacy and reachability** considerations

### 3. Connection Attempts (RFC 8305 Section 5)

```
Start first connection attempt
    ↓
Wait ConnectionAttemptDelay (250ms)
    ↓
Start second connection attempt
    ↓
Wait ConnectionAttemptDelay (250ms)
    ↓
Start third connection attempt
    ↓
... (continue until one succeeds or all fail)
```

The first successful connection wins, and all other attempts are cancelled.

## Example Output

```
Happy Eyeballs v2 (RFC 8305) Sample Application
=================================================

Example 1: Basic Connection
---------------------------
✓ Successfully connected!
  Connected to: 2607:f8b0:4004:c07::71
  Address family: InterNetworkV6
  Connection time: 45.23ms
  Attempted addresses: 3
  HTTP Response: HTTP/1.1 200 OK
```

## Architecture

The library consists of four main components:

### 1. HappyEyeballsConnection
The main entry point that orchestrates the connection process.

### 2. HappyEyeballsConnectionSettings
Configuration options for customizing behavior.

### 3. AddressSorter
Implements RFC 6724 destination address selection.

### 4. ConnectionAttemptResult
Contains the result of connection attempts, including:
- Success/failure status
- Connected socket
- Connected IP address
- List of attempted addresses
- Connection timing
- Exception details (if failed)

## Running the Sample

```bash
cd samples/HappyEyeballs.Sample
dotnet run
```

The sample demonstrates:
1. Basic connection with default settings
2. Connection with custom settings and logging
3. Comparing connections to multiple hosts

## RFC 8305 Compliance

This implementation follows RFC 8305 specifications:

- ✅ **Section 3**: Resolution Delay for DNS queries
- ✅ **Section 4**: Sorting Addresses (follows RFC 6724)
- ✅ **Section 5**: Connection Attempt Delay
- ✅ **Section 6**: Preference for IPv6
- ✅ **Section 7**: Handling Multiple Addresses

## Use Cases

This library is ideal for:

- Applications connecting to dual-stack services
- Network tools and diagnostics
- Clients that need fast, reliable connections regardless of IPv4/IPv6 availability
- Services running in mixed IPv4/IPv6 environments
- Applications targeting global audiences with varying network conditions

## Performance Considerations

- **Fast connections**: First successful connection returns immediately
- **Low overhead**: Minimal memory allocation, reuses sockets
- **Cancellation support**: All operations respect cancellation tokens
- **No blocking**: Fully asynchronous implementation

## Building

Build the entire solution:

```bash
dotnet build HappyEyeballs.sln
```

Build individual projects:

```bash
# Library
dotnet build src/HappyEyeballs/HappyEyeballs.csproj

# Sample
dotnet build samples/HappyEyeballs.Sample/HappyEyeballs.Sample.csproj
```

## Requirements

- .NET 10.0 or later
- Supports Windows, Linux, and macOS

## License

This project is provided as-is for educational and commercial use.

## References

- [RFC 8305 - Happy Eyeballs Version 2](https://tools.ietf.org/html/rfc8305)
- [RFC 6724 - Default Address Selection for IPv6](https://tools.ietf.org/html/rfc6724)
- [RFC 6555 - Happy Eyeballs (Version 1)](https://tools.ietf.org/html/rfc6555)

## Contributing

Contributions are welcome! Please ensure:
- Code follows existing style
- RFC compliance is maintained
- Tests pass (if applicable)
- Documentation is updated

## Support

For issues, questions, or contributions, please open an issue or pull request.

---

**Made with ❤️ for better dual-stack connectivity**
