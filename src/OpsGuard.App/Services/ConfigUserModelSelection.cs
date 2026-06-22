using OpsGuard.Core.Configuration;

namespace OpsGuard.App.Services;

/// <summary>CLI 等无 UI 场景：模型固定为配置文件中的 ModelId。</summary>
public sealed class ConfigUserModelSelection(LlmOptions llmOptions) : IUserModelSelection
{
    public string ModelId { get; private set; } = LlmModelCatalog.NormalizeOrDefault(llmOptions.ModelId);

    public void SetModelId(string modelId) =>
        ModelId = LlmModelCatalog.NormalizeOrThrow(modelId);
}
