using System.ComponentModel;
using Microsoft.SemanticKernel;
using OpsGuard.Core.Serialization;
using OpsGuard.Core.Topology;
using OpsGuard.Infrastructure.Docker;

namespace OpsGuard.Plugins.Compose;

public sealed class ComposeStatusPlugin
{
    private readonly IServiceCatalog _serviceCatalog;
    private readonly IComposeDockerClient _dockerClient;

    public ComposeStatusPlugin(IServiceCatalog serviceCatalog, IComposeDockerClient dockerClient)
    {
        _serviceCatalog = serviceCatalog;
        _dockerClient = dockerClient;
    }

    [KernelFunction("GetComposeServiceStatus")]
    [Description("查询 JSON 拓扑中指定 serviceId 对应容器的运行状态。")]
    public async Task<string> GetComposeServiceStatusAsync(
        [Description("拓扑 JSON 中的服务 Id，例如 backend")] string serviceId,
        CancellationToken cancellationToken = default)
    {
        if (!_serviceCatalog.TryGetValidatedContainerName(serviceId, out var containerName) || containerName is null)
        {
            return DiagnosticJson.Serialize(new
            {
                success = false,
                error = $"Unknown serviceId '{serviceId}'. Call GetComposeTopology first."
            });
        }

        var result = await _dockerClient.InspectAsync(containerName, serviceId, cancellationToken);
        return DiagnosticJson.Serialize(result);
    }
}
