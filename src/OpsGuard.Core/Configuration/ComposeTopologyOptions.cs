namespace OpsGuard.Core.Configuration;

public sealed class ComposeTopologyOptions
{
    public const string SectionName = "ComposeTopology";

    public string TopologyFile { get; set; } = "docs/compose-topology.sample.json";
}
