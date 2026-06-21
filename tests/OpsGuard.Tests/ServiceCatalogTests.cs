using FluentAssertions;
using OpsGuard.Core.Models;
using OpsGuard.Core.Topology;

namespace OpsGuard.Tests;

public class ServiceCatalogTests
{
    private readonly IServiceCatalog _catalog;

    public ServiceCatalogTests()
    {
        var topology = new ComposeTopology
        {
            ComposeProjectName = "demo",
            Services =
            [
                new ComposeServiceDefinition { Id = "backend", ContainerName = "demo-backend-1" },
                new ComposeServiceDefinition { Id = "redis", ContainerName = "demo-redis-1" }
            ]
        };

        _catalog = new ServiceCatalog(new FakeTopologyProvider(topology));
    }

    [Fact]
    public void TryGetService_KnownId_ReturnsService()
    {
        var ok = _catalog.TryGetService("backend", out var service);

        ok.Should().BeTrue();
        service!.ContainerName.Should().Be("demo-backend-1");
    }

    [Fact]
    public void TryGetService_UnknownId_ReturnsFalse()
    {
        var ok = _catalog.TryGetService("unknown", out var service);

        ok.Should().BeFalse();
        service.Should().BeNull();
    }

    [Fact]
    public void TryGetValidatedContainerName_KnownId_ReturnsValidatedName()
    {
        var ok = _catalog.TryGetValidatedContainerName("redis", out var containerName);

        ok.Should().BeTrue();
        containerName!.Value.Should().Be("demo-redis-1");
    }

    private sealed class FakeTopologyProvider : IComposeTopologyProvider
    {
        public FakeTopologyProvider(ComposeTopology topology) => Topology = topology;

        public ComposeTopology Topology { get; }
    }
}
