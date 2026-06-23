using System.ComponentModel;
using Microsoft.SemanticKernel;
using OpsGuard.Core.Serialization;
using OpsGuard.Core.Topology;

namespace OpsGuard.Plugins.Compose;

public sealed class ComposeTopologyPlugin
{
    private readonly IServiceCatalog _serviceCatalog;
    private readonly ITopologyOverlayProvider _overlayProvider;

    public ComposeTopologyPlugin(IServiceCatalog serviceCatalog, ITopologyOverlayProvider overlayProvider)
    {
        _serviceCatalog = serviceCatalog;
        _overlayProvider = overlayProvider;
    }

    [KernelFunction("DiscoverDockerServices")]
    [Description("刷新 docker ps 自动发现结果，并返回与 overlay 合并后的可检查服务清单。")]
    public Task<string> DiscoverDockerServicesAsync(CancellationToken cancellationToken = default) =>
        BuildTopologyPayloadAsync(cancellationToken);

    [KernelFunction("GetComposeTopology")]
    [Description("返回当前可检查服务清单（docker ps 自动发现 + 可选 overlay JSON）。")]
    public Task<string> GetComposeTopologyAsync(CancellationToken cancellationToken = default) =>
        BuildTopologyPayloadAsync(cancellationToken);

    private async Task<string> BuildTopologyPayloadAsync(CancellationToken cancellationToken)
    {
        await _serviceCatalog.RefreshAsync(cancellationToken);

        var payload = new
        {
            source = "docker-discovery+overlay",
            overlayLoaded = _overlayProvider.IsLoaded,
            overlayFile = _overlayProvider.OverlayFilePath,
            refreshedAt = _serviceCatalog.LastRefreshedAt,
            composeProjectName = _serviceCatalog.Topology.ComposeProjectName,
            host = _serviceCatalog.Topology.Host.DisplayName,
            services = _serviceCatalog.GetAllServices().Select(service => new
            {
                id = service.Id,
                containerName = service.ContainerName,
                composeService = service.ComposeService,
                displayName = service.DisplayName ?? service.Id,
                hasHealthUrl = !string.IsNullOrWhiteSpace(service.HealthUrl),
                healthUrl = service.HealthUrl,
                description = service.Description
            })
        };

        return DiagnosticJson.Serialize(payload);
    }
}
