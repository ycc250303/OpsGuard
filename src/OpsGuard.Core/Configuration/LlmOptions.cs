namespace OpsGuard.Core.Configuration;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string ModelId { get; set; } = "qwen3.6-plus";

    public string Endpoint { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";

    public string ApiKey { get; set; } = string.Empty;
}
