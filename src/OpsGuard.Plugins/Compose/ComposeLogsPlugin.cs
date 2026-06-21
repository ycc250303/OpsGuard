using System.ComponentModel;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Serialization;
using OpsGuard.Core.Topology;
using OpsGuard.Infrastructure.Docker;

namespace OpsGuard.Plugins.Compose;

public sealed class ComposeLogsPlugin
{
    private readonly IServiceCatalog _serviceCatalog;
    private readonly IComposeDockerClient _dockerClient;
    private readonly AgentOptions _options;

    public ComposeLogsPlugin(
        IServiceCatalog serviceCatalog,
        IComposeDockerClient dockerClient,
        IOptions<AgentOptions> options)
    {
        _serviceCatalog = serviceCatalog;
        _dockerClient = dockerClient;
        _options = options.Value;
    }

    [KernelFunction("QueryComposeServiceLogs")]
    [Description("获取 JSON 拓扑中指定 serviceId 对应容器的 docker logs 尾部日志。")]
    public async Task<string> QueryComposeServiceLogsAsync(
        [Description("拓扑 JSON 中的服务 Id，例如 backend")] string serviceId,
        [Description("日志行数，最大 200")] int tailLines = 100,
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

        var clampedTail = Math.Clamp(tailLines, 1, _options.MaxLogTailLines);
        var result = await _dockerClient.GetLogsAsync(containerName, serviceId, clampedTail, cancellationToken);
        return DiagnosticJson.Serialize(new
        {
            result.Success,
            serviceId,
            tailLines = clampedTail,
            logs = result.Data,
            error = result.Error
        });
    }
}
