using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

public sealed class ServiceCatalog : IServiceCatalog
{
    private readonly Dictionary<string, ComposeServiceDefinition> _services;

    public ServiceCatalog(IComposeTopologyProvider topologyProvider)
    {
        ArgumentNullException.ThrowIfNull(topologyProvider);
        Topology = topologyProvider.Topology;
        _services = Topology.Services.ToDictionary(s => s.Id, s => s, StringComparer.OrdinalIgnoreCase);
    }

    public ComposeTopology Topology { get; }

    public IReadOnlyList<ComposeServiceDefinition> GetAllServices() => Topology.Services;

    public bool TryGetService(string serviceId, out ComposeServiceDefinition? service)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            service = null;
            return false;
        }

        return _services.TryGetValue(serviceId.Trim(), out service);
    }

    public bool TryGetValidatedContainerName(string serviceId, out ValidatedContainerName? containerName)
    {
        containerName = null;
        if (!TryGetService(serviceId, out var service) || service is null)
        {
            return false;
        }

        containerName = new ValidatedContainerName(service.ContainerName);
        return true;
    }
}
