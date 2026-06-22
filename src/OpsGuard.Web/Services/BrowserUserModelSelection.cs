using Microsoft.JSInterop;
using OpsGuard.App.Services;
using OpsGuard.Core.Configuration;

namespace OpsGuard.Web.Services;

public sealed class BrowserUserModelSelection : IUserModelSelection
{
    private readonly IJSRuntime _js;
    private string _modelId;
    private bool _initialized;

    public BrowserUserModelSelection(IJSRuntime js, LlmOptions llmOptions)
    {
        _js = js;
        _modelId = LlmModelCatalog.NormalizeOrDefault(llmOptions.ModelId);
    }

    public string ModelId => _modelId;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            var stored = await _js.InvokeAsync<string?>("opsGuardSettings.getModelId");
            if (!string.IsNullOrWhiteSpace(stored))
            {
                _modelId = LlmModelCatalog.NormalizeOrDefault(stored);
            }
        }
        catch (JSException)
        {
            // 首次加载或脚本未就绪时沿用默认值
        }

        _initialized = true;
    }

    public void SetModelId(string modelId)
    {
        _modelId = LlmModelCatalog.NormalizeOrThrow(modelId);
        _ = PersistAsync(_modelId);
    }

    private async Task PersistAsync(string modelId)
    {
        try
        {
            await _js.InvokeVoidAsync("opsGuardSettings.setModelId", modelId);
        }
        catch (JSException)
        {
            // 忽略持久化失败，当前会话仍生效
        }
    }
}
