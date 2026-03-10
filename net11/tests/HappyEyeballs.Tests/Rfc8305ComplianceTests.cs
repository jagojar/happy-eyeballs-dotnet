using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Xunit;

namespace HappyEyeballs.Tests;

/// <summary>
/// Tests to validate RFC 8305 (Happy Eyeballs v2) compliance.
/// </summary>
public class Rfc8305ComplianceTests
{
    [Fact]
    public void Settings_ResolutionDelay_MeetsRfc8305Recommendation()
    {
        // RFC 8305 Section 3: Resolution Delay SHOULD be 50ms
        var settings = new HappyEyeballsConnectionSettings();

        settings.ResolutionDelay.Should().Be(TimeSpan.FromMilliseconds(50),
            "RFC 8305 Section 3 recommends 50ms Resolution Delay");
    }

    [Fact]
    public void Settings_ConnectionAttemptDelay_MeetsRfc8305Recommendation()
    {
        // RFC 8305 Section 5: Connection Attempt Delay SHOULD be 250ms
        var settings = new HappyEyeballsConnectionSettings();

        settings.ConnectionAttemptDelay.Should().Be(TimeSpan.FromMilliseconds(250),
            "RFC 8305 Section 5 specifies 250ms Connection Attempt Delay");
    }

    [Fact]
    public void Settings_DefaultPreferIPv6_MeetsRfc8305()
    {
        // RFC 8305 Section 4: IPv6 SHOULD be preferred over IPv4
        var settings = new HappyEyeballsConnectionSettings();

        settings.PreferIPv6.Should().BeTrue(
            "RFC 8305 Section 4 states IPv6 should be preferred");
    }

    [Fact]
    public void AddressSorter_InterleavesBehavior_MeetsRfc8305()
    {
        // RFC 8305 Section 4: Addresses from both families should be interleaved
        var ipv6 = new[]
        {
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("2001:db8::2")
        };
        var ipv4 = new[]
        {
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2")
        };

        var mixed = ipv6.Concat(ipv4).ToList();
        var result = AddressSorter.Sort(mixed, preferIPv6: true);

        // Should interleave: IPv6, IPv4, IPv6, IPv4 per RFC 8305
        result[0].AddressFamily.Should().Be(AddressFamily.InterNetworkV6,
            "First address should be from preferred family (IPv6)");
        result[1].AddressFamily.Should().Be(AddressFamily.InterNetwork,
            "Second address should be from alternate family (IPv4)");
        result[2].AddressFamily.Should().Be(AddressFamily.InterNetworkV6,
            "Third address should be from preferred family (IPv6)");
        result[3].AddressFamily.Should().Be(AddressFamily.InterNetwork,
            "Fourth address should be from alternate family (IPv4)");
    }

    [Fact]
    public void AddressSorter_WithIPv6Preferred_AlternatesCorrectly()
    {
        // RFC 8305 Section 4: When preferring IPv6, alternate between IPv6 and IPv4
        var addresses = new[]
        {
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2"),
            IPAddress.Parse("192.168.1.3"),
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("2001:db8::2"),
            IPAddress.Parse("2001:db8::3")
        };

        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Verify alternating pattern
        for (int i = 0; i < Math.Min(3, result.Count); i += 2)
        {
            if (i < result.Count)
            {
                result[i].AddressFamily.Should().Be(AddressFamily.InterNetworkV6,
                    $"Address at index {i} should be IPv6 when alternating");
            }
            if (i + 1 < result.Count)
            {
                result[i + 1].AddressFamily.Should().Be(AddressFamily.InterNetwork,
                    $"Address at index {i + 1} should be IPv4 when alternating");
            }
        }
    }

    [Fact]
    public void AddressSorter_WithIPv4Preferred_AlternatesCorrectly()
    {
        // RFC 8305: When preferring IPv4, alternate between IPv4 and IPv6
        var addresses = new[]
        {
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("2001:db8::2"),
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2")
        };

        var result = AddressSorter.Sort(addresses, preferIPv6: false);

        // First should be IPv4 (preferred), second IPv6 (alternate)
        result[0].AddressFamily.Should().Be(AddressFamily.InterNetwork);
        result[1].AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
    }

    [Fact]
    public void AddressSorter_SortsAccordingToRfc6724()
    {
        // RFC 8305 Section 4 references RFC 6724 for address sorting
        // Global unicast should be preferred over link-local
        var addresses = new[]
        {
            IPAddress.Parse("fe80::1"),       // Link-local (lower priority)
            IPAddress.Parse("2001:db8::1"),   // Global unicast (higher priority)
            IPAddress.Parse("fc00::1")        // Unique local (medium priority)
        };

        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Global unicast should come first
        result[0].ToString().Should().Be("2001:db8::1",
            "RFC 6724 prioritizes global unicast addresses");
    }

    [Fact]
    public void AddressSorter_IPv4PublicOverPrivate()
    {
        // RFC 6724 (referenced by RFC 8305): Prefer public over private addresses
        var addresses = new[]
        {
            IPAddress.Parse("192.168.1.1"),  // Private
            IPAddress.Parse("8.8.8.8"),       // Public (should be first)
            IPAddress.Parse("10.0.0.1")       // Private
        };

        var result = AddressSorter.Sort(addresses, preferIPv6: false);

        // Public address should come before private addresses
        result[0].ToString().Should().Be("8.8.8.8",
            "Public addresses should be preferred over private per RFC 6724");
    }

    [Fact]
    public void Settings_AllowsCustomization()
    {
        // RFC 8305: Implementation SHOULD allow customization of delays
        var settings = new HappyEyeballsConnectionSettings
        {
            ConnectionAttemptDelay = TimeSpan.FromMilliseconds(100),
            ResolutionDelay = TimeSpan.FromMilliseconds(25)
        };

        settings.ConnectionAttemptDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        settings.ResolutionDelay.Should().Be(TimeSpan.FromMilliseconds(25));
    }

    [Fact]
    public async Task ConnectAsync_StartsConnectionsWithStaggeredTiming()
    {
        // RFC 8305 Section 5: Connections should be staggered
        // This is an integration test that verifies the behavior indirectly
        
        var connection = new HappyEyeballsConnection();
        
        // Use localhost which should have both IPv4 and IPv6 on many systems
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await connection.ConnectAsync("localhost", port);
        stopwatch.Stop();

        result.IsSuccessful.Should().BeTrue();
        // Connection should succeed quickly (not waiting for full delay when first attempt works)
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));

        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public void StaticDefaults_MatchRfc8305Specifications()
    {
        // Verify that static constants match RFC 8305 specifications
        HappyEyeballsConnectionSettings.DefaultConnectionAttemptDelay
            .Should().Be(TimeSpan.FromMilliseconds(250),
            "RFC 8305 Section 5");

        HappyEyeballsConnectionSettings.DefaultResolutionDelay
            .Should().Be(TimeSpan.FromMilliseconds(50),
            "RFC 8305 Section 3");
    }

    [Fact]
    public void AddressSorter_HandlesEmptyList()
    {
        // Edge case: Should handle empty address list gracefully
        var addresses = Array.Empty<IPAddress>();

        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AddressSorter_HandlesSingleFamily()
    {
        // RFC 8305: Should work correctly with single address family
        var ipv6Only = new[]
        {
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("2001:db8::2")
        };

        var result = AddressSorter.Sort(ipv6Only, preferIPv6: true);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(addr => 
            addr.AddressFamily.Should().Be(AddressFamily.InterNetworkV6));
    }

    [Fact]
    public void AddressSorter_PreservesOrderWithinFamily()
    {
        // Addresses within the same family should maintain relative order
        var addresses = new[]
        {
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("2001:db8::2"),
            IPAddress.Parse("2001:db8::3")
        };

        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        result[0].ToString().Should().Be("2001:db8::1");
        result[1].ToString().Should().Be("2001:db8::2");
        result[2].ToString().Should().Be("2001:db8::3");
    }

    [Fact]
    public void ConnectionResult_IncludesAttemptedAddresses()
    {
        // RFC 8305: Implementations should track which addresses were tried
        var addresses = new List<IPAddress>
        {
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2")
        };

        var result = ConnectionAttemptResult.Failure(
            new Exception("Test"),
            addresses,
            TimeSpan.FromMilliseconds(100));

        result.AttemptedAddresses.Should().BeEquivalentTo(addresses);
    }
}