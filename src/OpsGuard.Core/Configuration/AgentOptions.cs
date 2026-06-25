namespace OpsGuard.Core.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int MaxLogTailLines { get; set; } = 200;

    /// <summary>日志 --since 相对时间最大回溯小时数（如 72h）。</summary>
    public int MaxLogSinceHours { get; set; } = 168;

    public int HttpProbeTimeoutSeconds { get; set; } = 10;

    public int OrchestrationTimeoutMinutes { get; set; } = 5;

    public int ConversationHistoryTurns { get; set; } = 6;

    /// <summary>仅发现带 Compose 项目标签的容器。</summary>
    public bool DockerDiscoveryComposeOnly { get; set; } = true;

    /// <summary>自动发现时排除的容器名前缀。</summary>
    public string[] DockerDiscoveryExcludeNamePrefixes { get; set; } = ["opsguard-"];

    /// <summary>Web 流式诊断 UI 刷新最小间隔（毫秒）。</summary>
    public int StreamUiNotifyIntervalMs { get; set; } = 250;
}
