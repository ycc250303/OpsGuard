using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

public sealed class DynamicServiceCatalog : IServiceCatalog
{
    private readonly IDockerContainerDiscovery _discovery;
    private readonly ITopologyOverlayProvider _overlayProvider;
    private readonly AgentOptions _options;
    private readonly ILogger<DynamicServiceCatalog> _logger;
    private readonly object _gate = new();
    private ComposeTopology _topology;
    private Dictionary<string, ComposeServiceDefinition> _services = new(StringComparer.OrdinalIgnoreCase);

    public DynamicServiceCatalog(
        IDockerContainerDiscovery discovery,
        ITopologyOverlayProvider overlayProvider,
        IOptions<AgentOptions> options,
        ILogger<DynamicServiceCatalog> logger)
    {
        _discovery = discovery;
        _overlayProvider = overlayProvider;
        _options = options.Value;
        _logger = logger;
        _topology = CreateEmptyTopology(overlayProvider.Overlay);
    }

    public ComposeTopology Topology
    {
        get
        {
            lock (_gate)
            {
                return _topology;
            }
        }
    }

    public DateTimeOffset? LastRefreshedAt { get; private set; }

    public IReadOnlyList<ComposeServiceDefinition> GetAllServices() => Topology.Services;

    public bool TryGetService(string serviceId, out ComposeServiceDefinition? service)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            service = null;
            return false;
        }

        lock (_gate)
        {
            return _services.TryGetValue(serviceId.Trim(), out service);
        }
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

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var discovered = await _discovery.DiscoverAsync(cancellationToken);
        var merged = TopologyMerger.Merge(discovered, _overlayProvider.Overlay, _options);

        lock (_gate)
        {
            _topology = merged;
            _services = merged.Services.ToDictionary(service => service.Id, service => service, StringComparer.OrdinalIgnoreCase);
            LastRefreshedAt = DateTimeOffset.UtcNow;
        }

        _logger.LogInformation(
            "Docker discovery refreshed: {Count} services (overlay loaded={OverlayLoaded})",
            merged.Services.Count,
            _overlayProvider.IsLoaded);
    }

    private static ComposeTopology CreateEmptyTopology(ComposeTopology overlay) => new()
    {
        ComposeProjectName = string.IsNullOrWhiteSpace(overlay.ComposeProjectName) ? "discovered" : overlay.ComposeProjectName,
        Host = overlay.Host,
        Services = []
    };
}
