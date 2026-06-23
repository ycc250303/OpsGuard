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
    [Description("获取已发现清单中 serviceId 对应容器的 docker logs。支持按时间起点过滤（--since）并带时间戳；仍受 tailLines 上限约束。")]
    public async Task<string> QueryComposeServiceLogsAsync(
        [Description("服务 Id，例如 backend")] string serviceId,
        [Description("日志行数上限，最大 200")] int tailLines = 100,
        [Description("仅返回该时间点之后的日志。相对时间：72h（三天）、24h、30m；或 ISO 时间如 2024-01-01T00:00:00Z。留空则只取尾部 tailLines 行")] string? since = null,
        CancellationToken cancellationToken = default)
    {
        if (!_serviceCatalog.TryGetValidatedContainerName(serviceId, out var containerName) || containerName is null)
        {
            return DiagnosticJson.Serialize(new
            {
                success = false,
                error = $"Unknown serviceId '{serviceId}'. Call GetComposeTopology or DiscoverDockerServices first."
            });
        }

        if (!DockerLogSinceValidator.TryNormalize(since, _options.MaxLogSinceHours, out var normalizedSince, out var sinceError))
        {
            return DiagnosticJson.Serialize(new
            {
                success = false,
                serviceId,
                error = sinceError
            });
        }

        var clampedTail = Math.Clamp(tailLines, 1, _options.MaxLogTailLines);
        var result = await _dockerClient.GetLogsAsync(
            containerName,
            serviceId,
            clampedTail,
            normalizedSince,
            cancellationToken);

        return DiagnosticJson.Serialize(new
        {
            result.Success,
            serviceId,
            tailLines = clampedTail,
            since = normalizedSince,
            timestamps = true,
            logs = result.Data,
            error = result.Error
        });
    }
}
