using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

public interface IDockerContainerDiscovery
{
    Task<IReadOnlyList<DiscoveredContainer>> DiscoverAsync(CancellationToken cancellationToken = default);
}
