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
}
