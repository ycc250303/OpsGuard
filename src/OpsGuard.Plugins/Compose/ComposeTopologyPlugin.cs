using System.ComponentModel;
using Microsoft.SemanticKernel;
using OpsGuard.Core.Serialization;
using OpsGuard.Core.Topology;

namespace OpsGuard.Plugins.Compose;

public sealed class ComposeTopologyPlugin
{
    private readonly IServiceCatalog _serviceCatalog;

    public ComposeTopologyPlugin(IServiceCatalog serviceCatalog)
    {
        _serviceCatalog = serviceCatalog;
    }

    [KernelFunction("GetComposeTopology")]
    [Description("返回 compose-topology.json 中全部可检查服务清单。")]
    public Task<string> GetComposeTopologyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = new
        {
            composeProjectName = _serviceCatalog.Topology.ComposeProjectName,
            host = _serviceCatalog.Topology.Host.DisplayName,
            services = _serviceCatalog.GetAllServices().Select(s => new
            {
                id = s.Id,
                displayName = s.DisplayName ?? s.Id,
                hasHealthUrl = !string.IsNullOrWhiteSpace(s.HealthUrl),
                description = s.Description
            })
        };

        return Task.FromResult(DiagnosticJson.Serialize(payload));
    }
}
