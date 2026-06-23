using OpsGuard.Core.Configuration;

namespace OpsGuard.Tests;

public sealed class LlmModelCatalogTests
{
    [Theory]
    [InlineData("qwen3.6-plus", "qwen3.6-plus")]
    [InlineData("qwen3.7-max", "qwen3.7-max")]
    [InlineData("deepseek-v4-flash", "deepseek-v4-flash")]
    [InlineData("deepseek-v4-pro", "deepseek-v4-pro")]
    [InlineData(" qwen3.7-max ", "qwen3.7-max")]
    public void NormalizeOrThrow_accepts_supported_models(string input, string expected) =>
        Assert.Equal(expected, LlmModelCatalog.NormalizeOrThrow(input));

    [Theory]
    [InlineData("deepseek-chat", "deepseek-v4-flash")]
    [InlineData("deepseek-reasoner", "deepseek-v4-pro")]
    public void NormalizeOrThrow_maps_legacy_deepseek_models(string input, string expected) =>
        Assert.Equal(expected, LlmModelCatalog.NormalizeOrThrow(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("qwen-plus")]
    public void NormalizeOrDefault_falls_back_for_unknown(string? input) =>
        Assert.Equal(LlmModelCatalog.Default, LlmModelCatalog.NormalizeOrDefault(input));

    [Fact]
    public void NormalizeOrThrow_rejects_unknown() =>
        Assert.Throws<ArgumentException>(() => LlmModelCatalog.NormalizeOrThrow("gpt-4"));

    [Fact]
    public void ResolveOptions_uses_deepseek_provider()
    {
        var options = LlmModelCatalog.ResolveOptions(LlmModelCatalog.DeepSeekV4Flash);
        Assert.Equal(LlmModelCatalog.DeepSeekV4Flash, options.ModelId);
        Assert.Equal("https://api.deepseek.com", options.Endpoint);
    }

    [Fact]
    public void GetProviderForModel_maps_qwen_to_dashscope()
    {
        var provider = LlmModelCatalog.GetProviderForModel(LlmModelCatalog.Plus);
        Assert.Equal(LlmModelCatalog.ProviderDashScope, provider.Id);
        Assert.Equal("DASHSCOPE_API_KEY", provider.ApiKeyEnvironmentVariable);
    }
}
