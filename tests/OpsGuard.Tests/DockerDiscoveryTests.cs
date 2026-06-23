using FluentAssertions;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Models;
using OpsGuard.Core.Topology;
using OpsGuard.Infrastructure.Docker;

namespace OpsGuard.Tests;

public sealed class TopologyMergerTests
{
    [Fact]
    public void Merge_AppliesOverlayAliasAndHealthUrl()
    {
        var discovered = new List<DiscoveredContainer>
        {
            new()
            {
                ContainerName = "zdmj-frontend-main",
                ComposeProject = "zdmj",
                ComposeService = "nginx",
                State = "running",
                Status = "Up"
            }
        };

        var overlay = new ComposeTopology
        {
            ComposeProjectName = "zdmj",
            Services =
            [
                new ComposeServiceDefinition
                {
                    Id = "web-gateway",
                    ContainerName = "zdmj-frontend-main",
                    HealthUrl = "http://127.0.0.1:80",
                    DisplayName = "前端 Nginx"
                }
            ]
        };

        var merged = TopologyMerger.Merge(discovered, overlay, new AgentOptions());

        merged.Services.Should().ContainSingle();
        merged.Services[0].Id.Should().Be("web-gateway");
        merged.Services[0].HealthUrl.Should().Be("http://127.0.0.1:80");
    }

    [Fact]
    public void Merge_FiltersByComposeProject()
    {
        var discovered = new List<DiscoveredContainer>
        {
            new()
            {
                ContainerName = "zdmj-backend",
                ComposeProject = "zdmj",
                ComposeService = "backend",
                State = "running",
                Status = "Up"
            },
            new()
            {
                ContainerName = "other-redis",
                ComposeProject = "other",
                ComposeService = "redis",
                State = "running",
                Status = "Up"
            }
        };

        var overlay = new ComposeTopology { ComposeProjectName = "zdmj", Services = [] };
        var merged = TopologyMerger.Merge(discovered, overlay, new AgentOptions());

        merged.Services.Should().ContainSingle(service => service.Id == "backend");
    }
}

public sealed class DockerContainerDiscoveryTests
{
    [Fact]
    public void ParseLines_ParsesComposeLabels()
    {
        var output = """
            {"Names":"zdmj-backend","State":"running","Status":"Up 1 hour","Labels":"com.docker.compose.project=zdmj,com.docker.compose.service=backend","Ports":"0.0.0.0:8080->8080/tcp","Image":"demo/backend"}
            """;

        var containers = DockerContainerDiscovery.ParseLines(output);

        containers.Should().ContainSingle();
        containers[0].ContainerName.Should().Be("zdmj-backend");
        containers[0].ComposeProject.Should().Be("zdmj");
        containers[0].ComposeService.Should().Be("backend");
    }
}

public sealed class TopologyOverlayProviderTests
{
    [Fact]
    public void MissingFile_ReturnsEmptyOverlay()
    {
        var provider = new TopologyOverlayProvider(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));

        provider.IsLoaded.Should().BeFalse();
        provider.Overlay.Services.Should().BeEmpty();
    }

    [Fact]
    public void LoadPartialOverlay_Succeeds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"overlay-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "ComposeProjectName": "zdmj",
              "Services": [
                {
                  "Id": "backend",
                  "ContainerName": "zdmj-backend",
                  "HealthUrl": "http://127.0.0.1:8080/actuator/health"
                }
              ]
            }
            """);

        try
        {
            var provider = new TopologyOverlayProvider(path);
            provider.IsLoaded.Should().BeTrue();
            provider.Overlay.Services.Should().ContainSingle(service => service.Id == "backend");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
