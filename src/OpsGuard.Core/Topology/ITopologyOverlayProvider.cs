using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

public interface ITopologyOverlayProvider
{
    string? OverlayFilePath { get; }

    bool IsLoaded { get; }

    ComposeTopology Overlay { get; }
}
