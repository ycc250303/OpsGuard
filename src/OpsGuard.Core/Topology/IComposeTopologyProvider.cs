using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

public interface IComposeTopologyProvider
{
    ComposeTopology Topology { get; }
}
