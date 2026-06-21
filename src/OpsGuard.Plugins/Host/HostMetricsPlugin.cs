using System.ComponentModel;
using Microsoft.SemanticKernel;
using OpsGuard.Core.Models;
using OpsGuard.Core.Serialization;
using OpsGuard.Core.Topology;
using OpsGuard.Infrastructure.Host;

namespace OpsGuard.Plugins.Host;

public sealed class HostMetricsPlugin
{
    private readonly IHostMetricsReader _hostMetricsReader;

    public HostMetricsPlugin(IHostMetricsReader hostMetricsReader)
    {
        _hostMetricsReader = hostMetricsReader;
    }

    [KernelFunction("GetHostMetrics")]
    [Description("获取 Linux 主机 CPU、内存、磁盘与负载指标。")]
    public async Task<string> GetHostMetricsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _hostMetricsReader.ReadAsync(cancellationToken);
        return DiagnosticJson.Serialize(result);
    }
}
