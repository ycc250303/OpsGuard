using OpsGuard.Core.Configuration;

namespace OpsGuard.Tests;

public sealed class LlmModelCatalogTests
{
    [Theory]
    [InlineData("qwen3.6-plus", "qwen3.6-plus")]
    [InlineData("qwen3.7-max", "qwen3.7-max")]
    [InlineData(" qwen3.7-max ", "qwen3.7-max")]
    public void NormalizeOrThrow_accepts_supported_models(string input, string expected) =>
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
}
