namespace OpsGuard.Core.Models;

public sealed class ComposeTopology
{
    public string ComposeProjectName { get; set; } = string.Empty;

    public HostInfo Host { get; set; } = new();

    public List<ComposeServiceDefinition> Services { get; set; } = [];
}
