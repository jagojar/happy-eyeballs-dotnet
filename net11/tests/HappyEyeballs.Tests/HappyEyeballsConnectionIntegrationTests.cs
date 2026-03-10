using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Xunit;

namespace HappyEyeballs.Tests;

public class HappyEyeballsConnectionIntegrationTests
{
    [Fact]
    public async Task ConnectAsync_ToLocalhost_Succeeds()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var connection = new HappyEyeballsConnection();

        // Act
        var result = await connection.ConnectAsync("localhost", port);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.Socket.Should().NotBeNull();
        result.ConnectedAddress.Should().NotBeNull();
        result.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
        result.Exception.Should().BeNull();

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public async Task ConnectAsync_ToNonExistentHost_Fails()
    {
        // Arrange
        var connection = new HappyEyeballsConnection();

        // Act
        var result = await connection.ConnectAsync("nonexistent.invalid.hostname.test.local.invalid", 80);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.Exception.Should().NotBeNull();
        result.Socket.Should().BeNull();
        result.ConnectedAddress.Should().BeNull();
    }

    [Fact]
    public async Task ConnectAsync_WithCustomTimeout_RespectsConfiguration()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(100),
            ConnectionAttemptDelay = TimeSpan.FromMilliseconds(50)
        };
        var connection = new HappyEyeballsConnection(settings);

        // Use a non-routable IP address (TEST-NET-1 from RFC 5737)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        var result = await connection.ConnectAsync("192.0.2.1", 80);
        stopwatch.Stop();

        // Assert
        result.IsSuccessful.Should().BeFalse();
        // Should timeout relatively quickly due to short timeout
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConnectAsync_WithIPv4Preference_WorksCorrectly()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings
        {
            PreferIPv6 = false
        };
        var connection = new HappyEyeballsConnection(settings);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Act
        var result = await connection.ConnectAsync("127.0.0.1", port);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.Socket.Should().NotBeNull();
        result.ConnectedAddress.Should().Be(IPAddress.Loopback);

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public async Task ConnectAsync_ToRefusedConnection_FailsGracefully()
    {
        // Arrange
        var connection = new HappyEyeballsConnection();
        
        // Use an unlikely port that should be closed
        var port = 60000 + Random.Shared.Next(5000);

        // Act
        var result = await connection.ConnectAsync("127.0.0.1", port);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.Exception.Should().NotBeNull();
        result.Socket.Should().BeNull();
    }

    [Fact]
    public async Task ConnectAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var connection = new HappyEyeballsConnection();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        Func<Task> act = async () => await connection.ConnectAsync("localhost", 80, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConnectAsync_WithDelayedCancellation_CancelsInProgress()
    {
        // Arrange
        var connection = new HappyEyeballsConnection();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Use a non-routable address to ensure connection takes time
        // Act
        Func<Task> act = async () => await connection.ConnectAsync("192.0.2.1", 80, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConnectAsync_MultipleSimultaneousCalls_WorkIndependently()
    {
        // Arrange
        using var listener1 = new TcpListener(IPAddress.Loopback, 0);
        using var listener2 = new TcpListener(IPAddress.Loopback, 0);
        listener1.Start();
        listener2.Start();
        var port1 = ((IPEndPoint)listener1.LocalEndpoint).Port;
        var port2 = ((IPEndPoint)listener2.LocalEndpoint).Port;

        var connection = new HappyEyeballsConnection();

        // Act
        var task1 = connection.ConnectAsync("localhost", port1);
        var task2 = connection.ConnectAsync("localhost", port2);
        var results = await Task.WhenAll(task1, task2);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.IsSuccessful.Should().BeTrue());
        results[0].Socket.Should().NotBeSameAs(results[1].Socket);

        // Cleanup
        foreach (var result in results)
        {
            result.Socket?.Dispose();
        }
        listener1.Stop();
        listener2.Stop();
    }

    [Fact]
    public async Task ConnectAsync_WithIPAddress_ConnectsDirectly()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var connection = new HappyEyeballsConnection();

        // Act
        var result = await connection.ConnectAsync("127.0.0.1", port);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.ConnectedAddress.Should().Be(IPAddress.Loopback);
        result.AttemptedAddresses.Should().Contain(IPAddress.Loopback);

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public async Task ConnectAsync_WithLogging_DoesNotThrow()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings
        {
            EnableLogging = true
        };
        var connection = new HappyEyeballsConnection(settings);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Act
        var result = await connection.ConnectAsync("localhost", port);

        // Assert
        result.IsSuccessful.Should().BeTrue();

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public async Task ConnectAsync_WithNullSettings_UsesDefaults()
    {
        // Arrange
        var connection = new HappyEyeballsConnection(null);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Act
        var result = await connection.ConnectAsync("localhost", port);

        // Assert
        result.IsSuccessful.Should().BeTrue();

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public async Task ConnectAsync_RecordsAttemptedAddresses()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var connection = new HappyEyeballsConnection();

        // Act
        var result = await connection.ConnectAsync("localhost", port);

        // Assert
        result.AttemptedAddresses.Should().NotBeEmpty();
        result.ConnectedAddress.Should().NotBeNull();
        result.AttemptedAddresses.Should().Contain(result.ConnectedAddress!);

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public async Task ConnectAsync_WithCustomTimeout_ReturnsTimeoutFailure()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(100)
        };
        var connection = new HappyEyeballsConnection(settings);

        // Act
        var result = await connection.ConnectAsync("192.0.2.1", 80);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.Exception.Should().BeOfType<TimeoutException>();
        result.AttemptedAddresses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConnectAsync_ToLocalhost_AttemptedAddressesContainBothFamilies()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var connection = new HappyEyeballsConnection();

        // Act
        var result = await connection.ConnectAsync("localhost", port);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.AttemptedAddresses.Should().Contain(address => address.AddressFamily == AddressFamily.InterNetwork);
        result.AttemptedAddresses.Should().Contain(address => address.AddressFamily == AddressFamily.InterNetworkV6);
        result.ConnectedAddress.Should().NotBeNull();
        result.AttemptedAddresses.Should().Contain(result.ConnectedAddress!);

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public async Task ConnectAsync_MeasuresElapsedTime()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var connection = new HappyEyeballsConnection();

        // Act
        var result = await connection.ConnectAsync("localhost", port);

        // Assert
        result.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
        result.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10)); // Reasonable upper bound

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public async Task ConnectAsync_WithDifferentHostFormats_Succeeds(string host)
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var connection = new HappyEyeballsConnection();

        // Act
        var result = await connection.ConnectAsync(host, port);

        // Assert
        result.IsSuccessful.Should().BeTrue();

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }

    [Fact]
    public async Task ConnectAsync_WithCustomDelays_UsesConfiguration()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings
        {
            ConnectionAttemptDelay = TimeSpan.FromMilliseconds(100),
            ResolutionDelay = TimeSpan.FromMilliseconds(25)
        };
        var connection = new HappyEyeballsConnection(settings);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Act
        var result = await connection.ConnectAsync("localhost", port);

        // Assert
        result.IsSuccessful.Should().BeTrue();

        // Cleanup
        result.Socket?.Dispose();
        listener.Stop();
    }
}