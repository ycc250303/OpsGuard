namespace OpsGuard.Core.Configuration;

public sealed record LlmModelOption(string Id, string DisplayName, string Description);

public static class LlmModelCatalog
{
    public const string Plus = "qwen3.6-plus";
    public const string Max = "qwen3.7-max";

    public static readonly IReadOnlyList<LlmModelOption> Supported =
    [
        new(Plus, "Qwen 3.6 Plus", "日常诊断，响应较快"),
        new(Max, "Qwen 3.7 Max", "复杂问题，推理更强")
    ];

    public static string Default => Plus;

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
