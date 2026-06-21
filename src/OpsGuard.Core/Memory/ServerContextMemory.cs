using OpsGuard.Core.Models;
using OpsGuard.Core.Topology;

namespace OpsGuard.Core.Memory;

public sealed class ServerContextMemory
{
    private readonly string _summary;

    public ServerContextMemory(IServiceCatalog serviceCatalog)
    {
        ArgumentNullException.ThrowIfNull(serviceCatalog);
        _summary = BuildSummary(serviceCatalog.Topology, OpsGuardRuntimeContext.Current());
    }

    public string BuildSummary() => _summary;

    public static string BuildSummary(ComposeTopology topology) =>
        BuildSummary(topology, OpsGuardRuntimeContext.Current());

    public static string BuildSummary(ComposeTopology topology, OpsGuardRuntimeFacts runtime)
    {
        var hostMetricsNote = runtime.HostMetricsSupported
            ? "支持（读取本机 /proc 等）"
            : $"不支持（当前为 {runtime.OsPlatform}，Host 指标仅 Linux 可用）";

        var lines = new List<string>
        {
            "## 运行环境（唯一检测范围）",
            $"- OpsGuard 进程运行于: **{runtime.MachineName}**",
            $"- 操作系统: {runtime.OsDescription} ({runtime.OsPlatform})",
            $"- Host 指标: {hostMetricsNote}",
            "- **所有采集工具只作用于上述本机**，不会 SSH 或远程连接其他 IP。",
            "- 拓扑 Host.DisplayName 仅为服务清单备注标签，**不等于**当前检测目标。",
            "",
            "## Compose 服务清单",
            $"- 拓扑标签: {topology.Host.DisplayName}",
            $"- Compose 项目: {topology.ComposeProjectName}",
            "- 可检查服务（须本机存在对应 Docker 容器）:"
        };

        foreach (var service in topology.Services)
        {
            var display = string.IsNullOrWhiteSpace(service.DisplayName) ? service.Id : service.DisplayName;
            var health = string.IsNullOrWhiteSpace(service.HealthUrl) ? "无 HTTP 探活" : $"HealthUrl={service.HealthUrl}";
            var description = string.IsNullOrWhiteSpace(service.Description) ? string.Empty : $" — {service.Description}";
            lines.Add($"  - `{service.Id}` ({display}): {health}{description}");
        }

        lines.Add("- 工具仅接受上述 serviceId，禁止查询未配置服务。");
        return string.Join(Environment.NewLine, lines);
    }
}
