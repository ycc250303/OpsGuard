using FluentAssertions;
using OpsGuard.Core.Memory;
using OpsGuard.Core.Models;
using OpsGuard.Core.Topology;

namespace OpsGuard.Tests;

public class ComposeTopologyProviderTests
{
    [Fact]
    public void Load_ValidTopology_Succeeds()
    {
        var path = WriteTempJson("""
            {
              "ComposeProjectName": "demo",
              "Host": { "DisplayName": "Test Host" },
              "Services": [
                {
                  "Id": "backend",
                  "ContainerName": "demo-backend-1"
                }
              ]
            }
            """);

        var provider = new ComposeTopologyProvider(path);

        provider.Topology.ComposeProjectName.Should().Be("demo");
        provider.Topology.Services.Should().ContainSingle(s => s.Id == "backend");
    }

    [Fact]
    public void Load_DuplicateServiceId_Throws()
    {
        var path = WriteTempJson("""
            {
              "ComposeProjectName": "demo",
              "Host": { "DisplayName": "Test Host" },
              "Services": [
                { "Id": "backend", "ContainerName": "a" },
                { "Id": "backend", "ContainerName": "b" }
              ]
            }
            """);

        var act = () => new ComposeTopologyProvider(path);
        act.Should().Throw<TopologyLoadException>().WithMessage("*Duplicate*");
    }

    [Fact]
    public void Load_MissingContainerName_Throws()
    {
        var path = WriteTempJson("""
            {
              "ComposeProjectName": "demo",
              "Host": { "DisplayName": "Test Host" },
              "Services": [
                { "Id": "backend", "ContainerName": "" }
              ]
            }
            """);

        var act = () => new ComposeTopologyProvider(path);
        act.Should().Throw<TopologyLoadException>();
    }

    private static string WriteTempJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsguard-topology-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
