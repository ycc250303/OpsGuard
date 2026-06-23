namespace OpsGuard.Core.Configuration;

public sealed record LlmProviderDefinition(
    string Id,
    string DisplayName,
    string Endpoint,
    string ApiKeyEnvironmentVariable);

public sealed record LlmModelOption(
    string Id,
    string DisplayName,
    string Description,
    string ProviderId);

public static class LlmModelCatalog
{
    public const string ProviderDashScope = "dashscope";
    public const string ProviderDeepSeek = "deepseek";

    public const string Plus = "qwen3.6-plus";
    public const string Max = "qwen3.7-max";
    public const string DeepSeekChat = "deepseek-chat";
    public const string DeepSeekReasoner = "deepseek-reasoner";

    public static readonly IReadOnlyList<LlmProviderDefinition> Providers =
    [
        new(
            ProviderDashScope,
            "阿里云百炼 Qwen",
            "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "DASHSCOPE_API_KEY"),
        new(
            ProviderDeepSeek,
            "DeepSeek",
            "https://api.deepseek.com",
            "DEEPSEEK_API_KEY")
    ];

    public static readonly IReadOnlyList<LlmModelOption> Supported =
    [
        new(Plus, "Qwen 3.6 Plus", "日常诊断，响应较快", ProviderDashScope),
        new(Max, "Qwen 3.7 Max", "复杂问题，推理更强", ProviderDashScope),
        new(DeepSeekChat, "DeepSeek Chat", "通用对话，性价比高", ProviderDeepSeek),
        new(DeepSeekReasoner, "DeepSeek Reasoner", "深度推理，适合复杂根因分析", ProviderDeepSeek)
    ];

    public static string Default => Plus;

    public static LlmProviderDefinition GetProvider(string providerId) =>
        Providers.First(provider => provider.Id.Equals(providerId, StringComparison.Ordinal));

    public static LlmProviderDefinition GetProviderForModel(string modelId)
    {
        var option = Supported.First(model => model.Id.Equals(NormalizeOrThrow(modelId), StringComparison.Ordinal));
        return GetProvider(option.ProviderId);
    }

    public static LlmOptions ResolveOptions(string modelId, LlmOptions? baseOptions = null)
    {
        var normalized = NormalizeOrThrow(modelId);
        var option = Supported.First(model => model.Id.Equals(normalized, StringComparison.Ordinal));
        var provider = GetProvider(option.ProviderId);

        var endpoint = provider.Id == ProviderDashScope
            && !string.IsNullOrWhiteSpace(baseOptions?.Endpoint)
            ? baseOptions!.Endpoint.Trim()
            : provider.Endpoint;

        var apiKey = Environment.GetEnvironmentVariable(provider.ApiKeyEnvironmentVariable) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey)
            && provider.Id == ProviderDashScope
            && !string.IsNullOrWhiteSpace(baseOptions?.ApiKey))
        {
            apiKey = baseOptions!.ApiKey;
        }

        return new LlmOptions
        {
            ModelId = normalized,
            Endpoint = endpoint,
            ApiKey = apiKey
        };
    }

    public static bool IsApiKeyConfigured(string modelId)
    {
        var options = ResolveOptions(NormalizeOrDefault(modelId));
        return !string.IsNullOrWhiteSpace(options.ApiKey);
    }

    public static string NormalizeOrDefault(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Default;
        }

        var trimmed = modelId.Trim();
        return IsSupported(trimmed) ? trimmed : Default;
    }

    public static string NormalizeOrThrow(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("请选择模型。", nameof(modelId));
        }

        var trimmed = modelId.Trim();
        if (!IsSupported(trimmed))
        {
            throw new ArgumentException(
                $"不支持的模型 \"{trimmed}\"，可选: {string.Join(", ", Supported.Select(m => m.Id))}。",
                nameof(modelId));
        }

        return trimmed;
    }

    public static bool IsSupported(string modelId) =>
        Supported.Any(option => option.Id.Equals(modelId, StringComparison.Ordinal));
}
