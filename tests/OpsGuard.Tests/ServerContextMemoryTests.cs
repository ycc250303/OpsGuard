using FluentAssertions;
using OpsGuard.Core.Memory;
using OpsGuard.Core.Models;

namespace OpsGuard.Tests;

public class ServerContextMemoryTests
{
    [Fact]
    public void BuildSummary_ContainsAllServiceIds()
    {
        var topology = new ComposeTopology
        {
            ComposeProjectName = "demo",
            Host = new HostInfo { DisplayName = "Demo Host" },
            Services =
            [
                new ComposeServiceDefinition
                {
                    Id = "backend",
                    DisplayName = "Backend",
                    HealthUrl = "http://127.0.0.1:8080/health"
                },
                new ComposeServiceDefinition { Id = "postgres", DisplayName = "DB" }
            ]
        };

        var runtime = new OpsGuardRuntimeFacts("test-mac", "Darwin", "macOS", IsLinux: false, HostMetricsSupported: false);
        var summary = ServerContextMemory.BuildSummary(topology, runtime);

        summary.Should().Contain("backend");
        summary.Should().Contain("postgres");
        summary.Should().Contain("Demo Host");
        summary.Should().Contain("HealthUrl=");
        summary.Should().Contain("test-mac");
        summary.Should().Contain("不会 SSH");
        summary.Should().Contain("拓扑标签");
    }
}
