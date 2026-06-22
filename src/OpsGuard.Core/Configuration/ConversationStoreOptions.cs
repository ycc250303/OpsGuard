namespace OpsGuard.Core.Configuration;

public sealed class ConversationStoreOptions
{
    public const string SectionName = "ConversationStore";

    /// <summary>会话 JSON 存储目录（绝对路径或相对 content root）。</summary>
    public string Directory { get; set; } = "data/conversations";
}
