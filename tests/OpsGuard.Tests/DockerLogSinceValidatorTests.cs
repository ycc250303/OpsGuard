using FluentAssertions;
using OpsGuard.Infrastructure.Docker;

namespace OpsGuard.Tests;

public class DockerLogSinceValidatorTests
{
    [Theory]
    [InlineData("72h", "72h")]
    [InlineData("24h", "24h")]
    [InlineData("30m", "30m")]
    [InlineData("2024-01-01", "2024-01-01")]
    [InlineData("2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z")]
    public void TryNormalize_AcceptsValidValues(string input, string expected)
    {
        var ok = DockerLogSinceValidator.TryNormalize(input, 168, out var normalized, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_AllowsEmpty(string? input)
    {
        var ok = DockerLogSinceValidator.TryNormalize(input, 168, out var normalized, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        normalized.Should().BeNull();
    }

    [Fact]
    public void TryNormalize_RejectsExcessiveLookback()
    {
        var ok = DockerLogSinceValidator.TryNormalize("200h", 168, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("168h");
    }

    [Fact]
    public void TryNormalize_RejectsUnsafeInput()
    {
        var ok = DockerLogSinceValidator.TryNormalize("72h; rm -rf /", 168, out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }
}
