using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Xunit;

namespace HappyEyeballs.Tests;

public class AddressSorterTests
{
    [Fact]
    public void Sort_WithIPv6Preferred_ReturnsIPv6First()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("10.0.0.1")
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Assert
        result.Should().HaveCount(3);
        result[0].AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
        result[1].AddressFamily.Should().Be(AddressFamily.InterNetwork);
        result[2].AddressFamily.Should().Be(AddressFamily.InterNetwork);
    }

    [Fact]
    public void Sort_WithIPv4Preferred_ReturnsIPv4First()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("2001:db8::2")
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: false);

        // Assert
        result.Should().HaveCount(3);
        result[0].AddressFamily.Should().Be(AddressFamily.InterNetwork);
        result[1].AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
        result[2].AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
    }

    [Fact]
    public void Sort_WithMultipleIPv6AndIPv4_InterleavesCorrectly()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2"),
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("2001:db8::2"),
            IPAddress.Parse("10.0.0.1")
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Assert - Should alternate IPv6, IPv4, IPv6, IPv4, IPv4
        result.Should().HaveCount(5);
        result[0].AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
        result[1].AddressFamily.Should().Be(AddressFamily.InterNetwork);
        result[2].AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
        result[3].AddressFamily.Should().Be(AddressFamily.InterNetwork);
        result[4].AddressFamily.Should().Be(AddressFamily.InterNetwork);
    }

    [Fact]
    public void Sort_WithOnlyIPv6_ReturnsAllIPv6()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("2001:db8::3"),
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("2001:db8::2")
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(addr => addr.AddressFamily.Should().Be(AddressFamily.InterNetworkV6));
    }

    [Fact]
    public void Sort_WithOnlyIPv4_ReturnsAllIPv4()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("192.168.1.3"),
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("10.0.0.1")
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(addr => addr.AddressFamily.Should().Be(AddressFamily.InterNetwork));
    }

    [Fact]
    public void Sort_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var addresses = Array.Empty<IPAddress>();

        // Act
        var result = AddressSorter.Sort(addresses);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Sort_IPv4Addresses_PrioritizesPublicOverPrivate()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("192.168.1.1"),  // Private
            IPAddress.Parse("8.8.8.8"),       // Public
            IPAddress.Parse("10.0.0.1"),      // Private
            IPAddress.Parse("1.1.1.1")        // Public
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: false);

        // Assert
        result.Should().HaveCount(4);
        // Public addresses should come before private
        result[0].ToString().Should().Be("8.8.8.8");
        result[1].ToString().Should().Be("1.1.1.1");
    }

    [Fact]
    public void Sort_IPv6Addresses_PrioritizesGlobalUnicast()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("fe80::1"),       // Link-local
            IPAddress.Parse("2001:db8::1"),   // Global unicast
            IPAddress.Parse("fc00::1")        // Unique local
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Assert
        result.Should().HaveCount(3);
        // Global unicast should be first
        result[0].ToString().Should().Be("2001:db8::1");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sort_PreservesAddressCount(bool preferIPv6)
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("10.0.0.1"),
            IPAddress.Parse("2001:db8::2"),
            IPAddress.Parse("8.8.8.8")
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6);

        // Assert
        result.Should().HaveCount(addresses.Length);
    }

    [Fact]
    public void Sort_WithMixedFamilies_MaintainsInternalOrder()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("2001:db8::2"),
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2")
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Assert
        // IPv6 addresses should maintain their order
        var ipv6Results = result.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6).ToList();
        ipv6Results[0].ToString().Should().Be("2001:db8::1");
        ipv6Results[1].ToString().Should().Be("2001:db8::2");

        // IPv4 addresses should maintain their order
        var ipv4Results = result.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();
        ipv4Results[0].ToString().Should().Be("192.168.1.1");
        ipv4Results[1].ToString().Should().Be("192.168.1.2");
    }

    [Fact]
    public void Sort_WithSingleAddress_ReturnsSameAddress()
    {
        // Arrange
        var addresses = new[] { IPAddress.Parse("192.168.1.1") };

        // Act
        var result = AddressSorter.Sort(addresses);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be(addresses[0]);
    }

    [Fact]
    public void Sort_WithLoopbackAddresses_HandlesCorrectly()
    {
        // Arrange
        var addresses = new[]
        {
            IPAddress.Loopback,       // 127.0.0.1
            IPAddress.IPv6Loopback,   // ::1
            IPAddress.Parse("8.8.8.8")
        };

        // Act
        var result = AddressSorter.Sort(addresses, preferIPv6: true);

        // Assert
        result.Should().HaveCount(3);
        result[0].AddressFamily.Should().Be(AddressFamily.InterNetworkV6); // ::1
        result[1].AddressFamily.Should().Be(AddressFamily.InterNetwork);    // 8.8.8.8
        result[2].AddressFamily.Should().Be(AddressFamily.InterNetwork);    // 127.0.0.1
    }
}