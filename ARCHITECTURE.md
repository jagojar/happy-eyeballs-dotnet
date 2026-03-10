# Happy Eyeballs v2 - Project Structure

## Directory Layout

```
happy-eyeballs-vs/
├── src/
│   └── HappyEyeballs/              # Library project
│       ├── HappyEyeballsConnection.cs
│       ├── HappyEyeballsConnectionSettings.cs
│       ├── ConnectionAttemptResult.cs
│       ├── AddressSorter.cs
│       └── HappyEyeballs.csproj
├── samples/
│   └── HappyEyeballs.Sample/       # Console sample application
│       ├── Program.cs
│       └── HappyEyeballs.Sample.csproj
├── README.md
├── ARCHITECTURE.md                  # This file
├── .gitignore
└── HappyEyeballs.slnx               # Solution file
```

## Component Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────┐
│                  Application Layer                       │
│          (Your code using the library)                   │
└───────────────────┬─────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────────┐
│           HappyEyeballsConnection                        │
│  • ConnectAsync()                                        │
│  • Orchestrates DNS resolution                           │
│  • Manages connection attempts                           │
│  • Handles cancellation                                  │
└───────┬─────────────┬──────────────┬────────────────────┘
        │             │              │
        ▼             ▼              ▼
┌───────────┐  ┌─────────────┐  ┌─────────────────────┐
│ DNS        │  │ Address     │  │ Connection          │
│ Resolution │  │ Sorter      │  │ Attempt Manager     │
│            │  │             │  │                     │
│ • A query  │  │ • RFC 6724  │  │ • Staggered timing  │
│ • AAAA     │  │   sorting   │  │ • Parallel attempts │
│   query    │  │ • IPv6      │  │ • First wins        │
│ • Parallel │  │   preference│  │ • Cleanup failed    │
└───────────┘  └─────────────┘  └─────────────────────┘
```

## Class Responsibilities

### HappyEyeballsConnection
**Main orchestrator** for the Happy Eyeballs v2 algorithm.

**Responsibilities:**
- Coordinates DNS resolution with Resolution Delay
- Sorts addresses using AddressSorter
- Manages parallel connection attempts with Connection Attempt Delay
- Returns first successful connection
- Cleans up failed attempts
- Respects cancellation tokens

**Key Methods:**
- `ConnectAsync(host, port, cancellationToken)` - Main entry point
- `ResolveAddressesAsync()` - DNS resolution with RFC 8305 timing
- `AttemptConnectionsAsync()` - Parallel connection attempts

### HappyEyeballsConnectionSettings
**Configuration** for connection behavior.

**Properties:**
- `ConnectionAttemptDelay` (250ms default) - RFC 8305 Section 5
- `ResolutionDelay` (50ms default) - RFC 8305 Section 3
- `PreferIPv6` (true default) - RFC 8305 Section 6
- `ConnectTimeout` (30s default) - Per-connection timeout
- `EnableLogging` - Debug logging toggle

### AddressSorter
**Implements RFC 6724** Destination Address Selection.

**Responsibilities:**
- Sort IPv6 addresses by scope and type
- Sort IPv4 addresses by reachability
- Interleave addresses from different families
- Apply address preference policy

**Algorithm:**
1. Separate IPv4 and IPv6 addresses
2. Sort each group by priority
3. Interleave based on PreferIPv6 setting

### ConnectionAttemptResult
**Result container** for connection attempts.

**Properties:**
- `IsSuccessful` - Connection status
- `Socket` - Connected socket (on success)
- `ConnectedAddress` - IP that succeeded
- `Exception` - Error details (on failure)
- `AttemptedAddresses` - All IPs tried
- `Elapsed` - Total connection time

## RFC 8305 Implementation

### Section 3: DNS Resolution

```csharp
// Start both A and AAAA queries in parallel
var ipv6Task = ResolveAddressFamilyAsync(host, IPv6);
var ipv4Task = ResolveAddressFamilyAsync(host, IPv4);

// Wait for first to complete
await Task.WhenAny(ipv6Task, ipv4Task);

// Wait ResolutionDelay for the second
await Task.WhenAny(secondQuery, Delay(ResolutionDelay));
```

### Section 5: Connection Attempt Delay

```csharp
foreach (var address in sortedAddresses)
{
    StartConnectionAttempt(address);
    
    if (not last address)
    {
        // Wait for delay or any completion
        await Task.WhenAny(
            Delay(ConnectionAttemptDelay),
            Task.WhenAny(allAttempts)
        );
    }
}
```

### Section 6: Sorting and Preference

```csharp
// Sort by RFC 6724, then interleave
var sorted = AddressSorter.Sort(addresses, preferIPv6);
// Result: [IPv6₁, IPv4₁, IPv6₂, IPv4₂, ...]
```

## Threading and Concurrency

### Async Pattern
All operations are fully asynchronous using `async/await`:
- No blocking on I/O
- Efficient thread pool usage
- Cancellation token support throughout

### Parallel Operations
1. **DNS queries**: A and AAAA run concurrently
2. **Connection attempts**: Staggered starts, all run in parallel
3. **First-wins**: First successful connection cancels others

### Resource Cleanup
- Failed sockets are disposed immediately
- Successful connection returns, others are cancelled and cleaned up
- Cancellation token propagates to all async operations

## Usage Flow

```
User calls ConnectAsync(host, port)
    ↓
Start parallel DNS resolution
    ↓
Wait for Resolution Delay
    ↓
Collect resolved addresses
    ↓
Sort addresses (RFC 6724)
    ↓
Start connection attempts with staggered timing
    ↓
First success → Cancel others → Return socket
OR
All fail → Return failure with exceptions
```

## Performance Characteristics

### Time Complexity
- **Best case**: Single successful connection (~50-100ms)
- **Average case**: 1-2 connection attempts (~100-350ms)
- **Worst case**: All addresses fail (~ConnectTimeout * NumAddresses)

### Space Complexity
- O(n) where n = number of resolved addresses
- Minimal allocations: Lists for addresses, Tasks for attempts
- Sockets created on-demand, disposed immediately on failure

### Network Characteristics
- **Latency**: Optimized for lowest achievable latency
- **Bandwidth**: Minimal overhead (only connection handshakes)
- **Fairness**: All addresses get fair chance with staggered timing

## Testing Strategy

### Unit Testing (Recommended additions)
- Mock DNS resolution
- Simulate network failures
- Test address sorting
- Verify cancellation behavior

### Integration Testing
- Test with actual dual-stack hosts
- Verify IPv4 fallback
- Test IPv6-only and IPv4-only scenarios
- Measure connection timing

### Sample Application
Demonstrates:
- Basic usage
- Custom configuration
- Multiple host connections
- Real-world scenarios

## Extension Points

### Custom Address Sorting
Extend `AddressSorter` for custom sorting logic:

```csharp
public class CustomAddressSorter : IAddressSorter
{
    public IReadOnlyList<IPAddress> Sort(...)
    {
        // Custom sorting logic
    }
}
```

### Custom Logging
Provide custom logger in settings:

```csharp
var settings = new HappyEyeballsConnectionSettings
{
    EnableLogging = true,
    Logger = (message) => myLogger.Info(message)
};
```

### Connection Customization
Override socket creation for custom options:

```csharp
protected virtual Socket CreateSocket(IPAddress address)
{
    var socket = new Socket(...);
    // Custom socket options
    return socket;
}
```

## Future Enhancements

### Potential Additions
1. **Metrics collection**: Track success rates, latency percentiles
2. **Address caching**: Cache working addresses for faster reconnection
3. **Adaptive delays**: Adjust timing based on network conditions
4. **Protocol support**: Extend to UDP, QUIC, etc.
5. **Service discovery**: Integrate with DNS-SD, mDNS

### Performance Optimizations
1. **Socket pooling**: Reuse sockets for same destination
2. **Connection reuse**: HTTP/2, HTTP/3 multiplexing
3. **Early data**: TLS 1.3 0-RTT support
4. **Happy Eyeballs v3**: Future RFC enhancements

## References

- [RFC 8305: Happy Eyeballs v2](https://tools.ietf.org/html/rfc8305)
- [RFC 6724: Default Address Selection for IPv6](https://tools.ietf.org/html/rfc6724)
- [RFC 6555: Happy Eyeballs v1](https://tools.ietf.org/html/rfc6555)
- [.NET Socket Programming](https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket)
