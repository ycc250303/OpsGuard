using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

public interface IServiceCatalog
{
    ComposeTopology Topology { get; }

    DateTimeOffset? LastRefreshedAt { get; }

    IReadOnlyList<ComposeServiceDefinition> GetAllServices();

    bool TryGetService(string serviceId, out ComposeServiceDefinition? service);

    bool TryGetValidatedContainerName(string serviceId, out ValidatedContainerName? containerName);

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
