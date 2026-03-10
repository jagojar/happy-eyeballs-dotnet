using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Xunit;

namespace HappyEyeballs.Tests;

public class ConnectionAttemptResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        // Arrange
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var address = IPAddress.Parse("192.168.1.1");
        var attemptedAddresses = new List<IPAddress> { address };
        var elapsed = TimeSpan.FromMilliseconds(100);

        // Act
        var result = ConnectionAttemptResult.Success(socket, address, attemptedAddresses, elapsed);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.Socket.Should().Be(socket);
        result.ConnectedAddress.Should().Be(address);
        result.Elapsed.Should().Be(elapsed);
        result.Exception.Should().BeNull();
        result.AttemptedAddresses.Should().BeEquivalentTo(attemptedAddresses);

        // Cleanup
        socket.Dispose();
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        // Arrange
        var exception = new SocketException(10061); // Connection refused
        var attemptedAddresses = new List<IPAddress> 
        { 
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2")
        };
        var elapsed = TimeSpan.FromMilliseconds(500);

        // Act
        var result = ConnectionAttemptResult.Failure(exception, attemptedAddresses, elapsed);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.Socket.Should().BeNull();
        result.ConnectedAddress.Should().BeNull();
        result.Exception.Should().Be(exception);
        result.Elapsed.Should().Be(elapsed);
        result.AttemptedAddresses.Should().BeEquivalentTo(attemptedAddresses);
    }

    [Fact]
    public void Success_WithEmptyAttemptedAddresses_Works()
    {
        // Arrange
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var address = IPAddress.Parse("192.168.1.1");
        var attemptedAddresses = Array.Empty<IPAddress>();
        var elapsed = TimeSpan.FromMilliseconds(50);

        // Act
        var result = ConnectionAttemptResult.Success(socket, address, attemptedAddresses, elapsed);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.AttemptedAddresses.Should().BeEmpty();

        // Cleanup
        socket.Dispose();
    }

    [Fact]
    public void Failure_WithEmptyAttemptedAddresses_Works()
    {
        // Arrange
        var exception = new Exception("Connection failed");
        var attemptedAddresses = Array.Empty<IPAddress>();
        var elapsed = TimeSpan.FromSeconds(1);

        // Act
        var result = ConnectionAttemptResult.Failure(exception, attemptedAddresses, elapsed);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.AttemptedAddresses.Should().BeEmpty();
    }

    [Fact]
    public void Success_WithMultipleAttemptedAddresses_PreservesAll()
    {
        // Arrange
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connectedAddress = IPAddress.Parse("192.168.1.3");
        var attemptedAddresses = new List<IPAddress>
        {
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2"),
            connectedAddress
        };
        var elapsed = TimeSpan.FromMilliseconds(300);

        // Act
        var result = ConnectionAttemptResult.Success(socket, connectedAddress, attemptedAddresses, elapsed);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.AttemptedAddresses.Should().HaveCount(3);
        result.AttemptedAddresses.Should().Contain(connectedAddress);

        // Cleanup
        socket.Dispose();
    }

    [Fact]
    public void Result_IsImmutableRecord()
    {
        // Arrange
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var address = IPAddress.Parse("192.168.1.1");
        var attemptedAddresses = new List<IPAddress> { address };
        var elapsed = TimeSpan.FromMilliseconds(100);

        // Act
        var result = ConnectionAttemptResult.Success(socket, address, attemptedAddresses, elapsed);

        // Assert - Verify it's a record with 'with' expression support
        var result2 = result with { Elapsed = TimeSpan.FromMilliseconds(200) };
        result2.Elapsed.Should().Be(TimeSpan.FromMilliseconds(200));
        result.Elapsed.Should().Be(TimeSpan.FromMilliseconds(100)); // Original unchanged

        // Cleanup
        socket.Dispose();
    }

    [Fact]
    public void Success_WithIPv6Address_Works()
    {
        // Arrange
        var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
        var address = IPAddress.Parse("2001:db8::1");
        var attemptedAddresses = new List<IPAddress> { address };
        var elapsed = TimeSpan.FromMilliseconds(75);

        // Act
        var result = ConnectionAttemptResult.Success(socket, address, attemptedAddresses, elapsed);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.ConnectedAddress.Should().Be(address);
        result.ConnectedAddress!.AddressFamily.Should().Be(AddressFamily.InterNetworkV6);

        // Cleanup
        socket.Dispose();
    }

    [Fact]
    public void Failure_WithDifferentExceptionTypes_PreservesExceptionType()
    {
        // Arrange
        var socketException = new SocketException(10060); // Connection timeout
        var timeoutException = new TimeoutException("Operation timed out");
        var operationCanceled = new OperationCanceledException("Canceled by user");

        // Act
        var result1 = ConnectionAttemptResult.Failure(socketException, Array.Empty<IPAddress>(), TimeSpan.Zero);
        var result2 = ConnectionAttemptResult.Failure(timeoutException, Array.Empty<IPAddress>(), TimeSpan.Zero);
        var result3 = ConnectionAttemptResult.Failure(operationCanceled, Array.Empty<IPAddress>(), TimeSpan.Zero);

        // Assert
        result1.Exception.Should().BeOfType<SocketException>();
        result2.Exception.Should().BeOfType<TimeoutException>();
        result3.Exception.Should().BeOfType<OperationCanceledException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void Result_PreservesElapsedTime(int milliseconds)
    {
        // Arrange
        var elapsed = TimeSpan.FromMilliseconds(milliseconds);
        var exception = new Exception("Test");

        // Act
        var result = ConnectionAttemptResult.Failure(exception, Array.Empty<IPAddress>(), elapsed);

        // Assert
        result.Elapsed.Should().Be(elapsed);
    }

    [Fact]
    public void AttemptedAddresses_IsReadOnly()
    {
        // Arrange
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var address = IPAddress.Parse("192.168.1.1");
        var attemptedAddresses = new List<IPAddress> { address };
        var elapsed = TimeSpan.FromMilliseconds(100);

        // Act
        var result = ConnectionAttemptResult.Success(socket, address, attemptedAddresses, elapsed);

        // Assert
        result.AttemptedAddresses.Should().BeAssignableTo<IReadOnlyList<IPAddress>>();

        // Cleanup
        socket.Dispose();
    }
}