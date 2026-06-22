using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Models;
using OpsGuard.Core.Topology;
using OpsGuard.Infrastructure.Docker;
using OpsGuard.Plugins.Compose;

namespace OpsGuard.Tests;

public class ComposeLogsPluginTests
{
    [Fact]
    public async Task QueryComposeServiceLogs_UnknownServiceId_ReturnsErrorJson()
    {
        var plugin = new ComposeLogsPlugin(
            CreateCatalog(),
            Mock.Of<IComposeDockerClient>(),
            Options.Create(new AgentOptions { MaxLogTailLines = 200 }));

        var json = await plugin.QueryComposeServiceLogsAsync("missing", 100);

        json.Should().Contain("Unknown serviceId");
    }

    [Fact]
    public async Task QueryComposeServiceLogs_ClampsTailLinesInRequest()
    {
        var docker = new Mock<IComposeDockerClient>();
        docker
            .Setup(d => d.GetLogsAsync(It.IsAny<ValidatedContainerName>(), "backend", 200, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DiagnosticResult<string>.Ok("logs"));

        var plugin = new ComposeLogsPlugin(
            CreateCatalog(),
            docker.Object,
            Options.Create(new AgentOptions { MaxLogTailLines = 200 }));

        await plugin.QueryComposeServiceLogsAsync("backend", 500);

        docker.Verify(d => d.GetLogsAsync(It.IsAny<ValidatedContainerName>(), "backend", 200, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryComposeServiceLogs_PassesSinceToDockerClient()
    {
        var docker = new Mock<IComposeDockerClient>();
        docker
            .Setup(d => d.GetLogsAsync(It.IsAny<ValidatedContainerName>(), "backend", 200, "72h", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DiagnosticResult<string>.Ok("2024-01-01 error"));

        var plugin = new ComposeLogsPlugin(
            CreateCatalog(),
            docker.Object,
            Options.Create(new AgentOptions { MaxLogTailLines = 200, MaxLogSinceHours = 168 }));

        var json = await plugin.QueryComposeServiceLogsAsync("backend", 200, "72h");

        json.Should().Contain("72h");
        docker.Verify(d => d.GetLogsAsync(It.IsAny<ValidatedContainerName>(), "backend", 200, "72h", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IServiceCatalog CreateCatalog()
    {
        var topology = new ComposeTopology
        {
            ComposeProjectName = "demo",
            Services = [new ComposeServiceDefinition { Id = "backend", ContainerName = "demo-backend-1" }]
        };

        return new ServiceCatalog(new FakeTopologyProvider(topology));
    }

    private sealed class FakeTopologyProvider(ComposeTopology topology) : IComposeTopologyProvider
    {
        public ComposeTopology Topology { get; } = topology;
    }
}
