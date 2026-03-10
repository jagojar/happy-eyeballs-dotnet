using FluentAssertions;
using Xunit;

namespace HappyEyeballs.Tests;

public class HappyEyeballsConnectionSettingsTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var settings = new HappyEyeballsConnectionSettings();

        // Assert
        settings.ConnectionAttemptDelay.Should().Be(TimeSpan.FromMilliseconds(250));
        settings.ResolutionDelay.Should().Be(TimeSpan.FromMilliseconds(50));
        settings.PreferIPv6.Should().BeTrue();
        settings.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(30));
        settings.EnableLogging.Should().BeFalse();
    }

    [Fact]
    public void StaticDefaultValues_MatchInstanceDefaults()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings();

        // Assert
        settings.ConnectionAttemptDelay.Should().Be(HappyEyeballsConnectionSettings.DefaultConnectionAttemptDelay);
        settings.ResolutionDelay.Should().Be(HappyEyeballsConnectionSettings.DefaultResolutionDelay);
    }

    [Fact]
    public void ConnectionAttemptDelay_CanBeSet()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings();
        var delay = TimeSpan.FromMilliseconds(500);

        // Act
        settings.ConnectionAttemptDelay = delay;

        // Assert
        settings.ConnectionAttemptDelay.Should().Be(delay);
    }

    [Fact]
    public void ResolutionDelay_CanBeSet()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings();
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        settings.ResolutionDelay = delay;

        // Assert
        settings.ResolutionDelay.Should().Be(delay);
    }

    [Fact]
    public void ConnectTimeout_CanBeSet()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings();
        var timeout = TimeSpan.FromSeconds(15);

        // Act
        settings.ConnectTimeout = timeout;

        // Assert
        settings.ConnectTimeout.Should().Be(timeout);
    }

    [Fact]
    public void PreferIPv6_CanBeSet()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings();

        // Act
        settings.PreferIPv6 = false;

        // Assert
        settings.PreferIPv6.Should().BeFalse();
    }

    [Fact]
    public void EnableLogging_CanBeSet()
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings();

        // Act
        settings.EnableLogging = true;

        // Assert
        settings.EnableLogging.Should().BeTrue();
    }

    [Fact]
    public void AllProperties_CanBeSetViaInitializer()
    {
        // Arrange & Act
        var settings = new HappyEyeballsConnectionSettings
        {
            ConnectionAttemptDelay = TimeSpan.FromMilliseconds(300),
            ResolutionDelay = TimeSpan.FromMilliseconds(75),
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PreferIPv6 = false,
            EnableLogging = true
        };

        // Assert
        settings.ConnectionAttemptDelay.Should().Be(TimeSpan.FromMilliseconds(300));
        settings.ResolutionDelay.Should().Be(TimeSpan.FromMilliseconds(75));
        settings.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(20));
        settings.PreferIPv6.Should().BeFalse();
        settings.EnableLogging.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void ConnectTimeout_AcceptsVariousValues(int seconds)
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings();
        var timeout = TimeSpan.FromSeconds(seconds);

        // Act
        settings.ConnectTimeout = timeout;

        // Assert
        settings.ConnectTimeout.Should().Be(timeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Delays_AcceptVariousMillisecondValues(int milliseconds)
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings();
        var delay = TimeSpan.FromMilliseconds(milliseconds);

        // Act
        settings.ConnectionAttemptDelay = delay;
        settings.ResolutionDelay = delay;

        // Assert
        settings.ConnectionAttemptDelay.Should().Be(delay);
        settings.ResolutionDelay.Should().Be(delay);
    }

    [Fact]
    public void DefaultConnectionAttemptDelay_Is250Milliseconds()
    {
        // RFC 8305 specifies 250ms as the default
        HappyEyeballsConnectionSettings.DefaultConnectionAttemptDelay
            .Should().Be(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void DefaultResolutionDelay_Is50Milliseconds()
    {
        // RFC 8305 specifies 50ms as the default
        HappyEyeballsConnectionSettings.DefaultResolutionDelay
            .Should().Be(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void Settings_CanBeClonedWithModifications()
    {
        // Arrange
        var original = new HappyEyeballsConnectionSettings
        {
            ConnectionAttemptDelay = TimeSpan.FromMilliseconds(100),
            EnableLogging = true
        };

        // Act
        var modified = new HappyEyeballsConnectionSettings
        {
            ConnectionAttemptDelay = original.ConnectionAttemptDelay,
            ResolutionDelay = original.ResolutionDelay,
            ConnectTimeout = TimeSpan.FromSeconds(10), // Different
            PreferIPv6 = original.PreferIPv6,
            EnableLogging = original.EnableLogging
        };

        // Assert
        modified.ConnectionAttemptDelay.Should().Be(original.ConnectionAttemptDelay);
        modified.ConnectTimeout.Should().NotBe(original.ConnectTimeout);
        modified.EnableLogging.Should().Be(original.EnableLogging);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PreferIPv6_AcceptsBothValues(bool prefer)
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings
        {
            PreferIPv6 = prefer
        };

        // Assert
        settings.PreferIPv6.Should().Be(prefer);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnableLogging_AcceptsBothValues(bool enable)
    {
        // Arrange
        var settings = new HappyEyeballsConnectionSettings
        {
            EnableLogging = enable
        };

        // Assert
        settings.EnableLogging.Should().Be(enable);
    }
}
