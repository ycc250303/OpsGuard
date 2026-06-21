namespace OpsGuard.Core.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int MaxLogTailLines { get; set; } = 200;

    public int HttpProbeTimeoutSeconds { get; set; } = 10;

    public int OrchestrationTimeoutMinutes { get; set; } = 5;

    public int ConversationHistoryTurns { get; set; } = 6;
}
