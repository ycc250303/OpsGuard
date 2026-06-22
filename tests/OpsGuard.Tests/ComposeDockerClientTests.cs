using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Models;
using OpsGuard.Infrastructure.Docker;
using OpsGuard.Infrastructure.Process;

namespace OpsGuard.Tests;

public class ComposeDockerClientTests
{
    [Fact]
    public async Task InspectAsync_UsesWhitelistDockerInspectArgs()
    {
        IReadOnlyList<string>? capturedArgs = null;
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(r => r.RunAsync("docker", It.IsAny<IReadOnlyList<string>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<string>, TimeSpan, CancellationToken>((_, args, _, _) => capturedArgs = args)
            .ReturnsAsync(new ProcessRunResult(0, """{"Status":"running","Running":true,"ExitCode":0,"RestartCount":0}""", string.Empty));

        var client = new ComposeDockerClient(
            runner.Object,
            Options.Create(new AgentOptions()),
            NullLogger<ComposeDockerClient>.Instance);

        var container = new ValidatedContainerName("demo-backend-1");
        var result = await client.InspectAsync(container, "backend");

        result.Success.Should().BeTrue();
        capturedArgs.Should().NotBeNull();
        capturedArgs![0].Should().Be("inspect");
        capturedArgs[1].Should().Be("demo-backend-1");
        capturedArgs.Should().NotContain("rm");
        capturedArgs.Should().NotContain("restart");
    }

    [Fact]
    public async Task GetLogsAsync_ClampsTailLines()
    {
        IReadOnlyList<string>? capturedArgs = null;
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(r => r.RunAsync("docker", It.IsAny<IReadOnlyList<string>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<string>, TimeSpan, CancellationToken>((_, args, _, _) => capturedArgs = args)
            .ReturnsAsync(new ProcessRunResult(0, "log line", string.Empty));

        var client = new ComposeDockerClient(
            runner.Object,
            Options.Create(new AgentOptions { MaxLogTailLines = 200 }),
            NullLogger<ComposeDockerClient>.Instance);

        var container = new ValidatedContainerName("demo-backend-1");
        await client.GetLogsAsync(container, "backend", 999);

        capturedArgs.Should().NotBeNull();
        capturedArgs![0].Should().Be("logs");
        capturedArgs[1].Should().Be("--timestamps");
        capturedArgs[2].Should().Be("--tail");
        capturedArgs[3].Should().Be("200");
        capturedArgs[4].Should().Be("demo-backend-1");
    }

    [Fact]
    public async Task GetLogsAsync_UsesSinceWhenProvided()
    {
        IReadOnlyList<string>? capturedArgs = null;
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(r => r.RunAsync("docker", It.IsAny<IReadOnlyList<string>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<string>, TimeSpan, CancellationToken>((_, args, _, _) => capturedArgs = args)
            .ReturnsAsync(new ProcessRunResult(0, "log line", string.Empty));

        var client = new ComposeDockerClient(
            runner.Object,
            Options.Create(new AgentOptions { MaxLogTailLines = 200, MaxLogSinceHours = 168 }),
            NullLogger<ComposeDockerClient>.Instance);

        var container = new ValidatedContainerName("demo-backend-1");
        await client.GetLogsAsync(container, "backend", 100, "72h");

        capturedArgs.Should().ContainInOrder("logs", "--timestamps", "--since", "72h", "--tail", "100", "demo-backend-1");
    }
}
