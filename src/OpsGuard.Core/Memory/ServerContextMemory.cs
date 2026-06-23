using OpsGuard.Core.Models;
using OpsGuard.Core.Topology;

namespace OpsGuard.Core.Memory;

public sealed class ServerContextMemory
{
    private readonly IServiceCatalog _serviceCatalog;
    private readonly ITopologyOverlayProvider _overlayProvider;
    private readonly string _baseSummary;

    public ServerContextMemory(IServiceCatalog serviceCatalog, ITopologyOverlayProvider overlayProvider)
    {
        _serviceCatalog = serviceCatalog;
        _overlayProvider = overlayProvider;
        _baseSummary = BuildBaseSummary(overlayProvider, OpsGuardRuntimeContext.Current());
    }

    public string BuildAgentContext() => _baseSummary;

    public string BuildLiveSummary() =>
        BuildSummary(_serviceCatalog.Topology, OpsGuardRuntimeContext.Current(), _overlayProvider, _serviceCatalog.LastRefreshedAt);

    public static string BuildSummary(ComposeTopology topology) =>
        BuildSummary(topology, OpsGuardRuntimeContext.Current(), null, null);

    public static string BuildSummary(
        ComposeTopology topology,
        OpsGuardRuntimeFacts runtime,
        ITopologyOverlayProvider? overlayProvider = null,
        DateTimeOffset? refreshedAt = null)
    {
        var hostMetricsNote = runtime.HostMetricsSupported
            ? "支持（读取本机 /proc 等）"
            : $"不支持（当前为 {runtime.OsPlatform}，Host 指标仅 Linux 可用）";

        var overlayNote = overlayProvider switch
        {
            { IsLoaded: true } => $"已加载（{_overlayPathHint(overlayProvider.OverlayFilePath)}）",
            { IsLoaded: false, OverlayFilePath: not null and not "" } => $"未找到文件 `{overlayProvider.OverlayFilePath}`，仅自动发现",
            _ => "未配置，仅自动发现"
        };

        var refreshNote = refreshedAt.HasValue
            ? refreshedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            : "尚未刷新";

        var lines = new List<string>
        {
            "## 运行环境（唯一检测范围）",
            $"- OpsGuard 进程运行于: **{runtime.MachineName}**",
            $"- 操作系统: {runtime.OsDescription} ({runtime.OsPlatform})",
            $"- Host 指标: {hostMetricsNote}",
            "- **所有采集工具只作用于上述本机**，不会 SSH 或远程连接其他 IP。",
            "",
            "## Compose 服务清单（docker ps 自动发现 + 可选 overlay）",
            "- 服务由 **docker ps** 实时发现；overlay JSON 可补充 HealthUrl、DisplayName、serviceId 别名。",
            "- **诊断前须调用 GetComposeTopology 或 DiscoverDockerServices** 获取当前 serviceId 列表。",
            $"- overlay: {overlayNote}",
            $"- 最近刷新: {refreshNote}",
            $"- Compose 项目: {topology.ComposeProjectName}",
            $"- 拓扑标签: {topology.Host.DisplayName}",
            "- 当前可检查服务:"
        };

        if (topology.Services.Count == 0)
        {
            lines.Add("  - （暂无，请先刷新发现或确认本机 Docker/Compose 容器在运行）");
        }
        else
        {
            foreach (var service in topology.Services)
            {
                var display = string.IsNullOrWhiteSpace(service.DisplayName) ? service.Id : service.DisplayName;
                var health = string.IsNullOrWhiteSpace(service.HealthUrl) ? "无 HTTP 探活" : $"HealthUrl={service.HealthUrl}";
                var container = string.IsNullOrWhiteSpace(service.ContainerName) ? string.Empty : $", container={service.ContainerName}";
                var description = string.IsNullOrWhiteSpace(service.Description) ? string.Empty : $" — {service.Description}";
                lines.Add($"  - `{service.Id}` ({display}{container}): {health}{description}");
            }
        }

        lines.Add("- 工具仅接受上述 serviceId，禁止查询未在清单中的容器。");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildBaseSummary(ITopologyOverlayProvider overlayProvider, OpsGuardRuntimeFacts runtime)
    {
        var hostMetricsNote = runtime.HostMetricsSupported
            ? "支持（读取本机 /proc 等）"
            : $"不支持（当前为 {runtime.OsPlatform}，Host 指标仅 Linux 可用）";

        return string.Join(Environment.NewLine,
        [
            "## 运行环境（唯一检测范围）",
            $"- OpsGuard 进程运行于: **{runtime.MachineName}**",
            $"- 操作系统: {runtime.OsDescription} ({runtime.OsPlatform})",
            $"- Host 指标: {hostMetricsNote}",
            "- **所有采集工具只作用于上述本机**，不会 SSH 或远程连接其他 IP。",
            "",
            "## Compose 服务",
            "- 清单由 docker ps 自动发现；overlay JSON 可选。",
            "- 每次诊断开始时会刷新服务列表；也可调用 GetComposeTopology / DiscoverDockerServices。"
        ]);
    }

    private static string _overlayPathHint(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "overlay";
        }

        return Path.GetFileName(path);
    }
}
