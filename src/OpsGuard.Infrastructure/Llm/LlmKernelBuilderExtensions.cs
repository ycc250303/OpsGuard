using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpsGuard.Core.Configuration;
using OpsGuard.Infrastructure.Network;

namespace OpsGuard.Infrastructure.Llm;

public static class LlmKernelBuilderExtensions
{
    public static IKernelBuilder AddOpsGuardChatCompletion(this IKernelBuilder builder, LlmOptions options)
    {
        var apiKey = string.IsNullOrWhiteSpace(options.ApiKey)
            ? Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY")
            : options.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "DASHSCOPE_API_KEY environment variable is required. Set it before starting OpsGuard.");
        }

        builder.AddOpenAIChatCompletion(
            modelId: options.ModelId,
            apiKey: apiKey,
            endpoint: new Uri(options.Endpoint),
            httpClient: OpsGuardLlmHttpClient.Instance);

        return builder;
    }

    public static OpenAIPromptExecutionSettings CreateToolCallingSettings() => new()
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    };

    public static OpenAIPromptExecutionSettings CreateChatOnlySettings() => new()
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.None()
    };

    public static Kernel BuildCollectorKernel(
        this IServiceProvider services,
        LlmOptions llmOptions,
        Action<IKernelBuilder>? configureBuilder = null)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(services.GetRequiredService<ILoggerFactory>());
        builder.AddOpsGuardChatCompletion(llmOptions);

        foreach (var filter in services.GetServices<IAutoFunctionInvocationFilter>())
        {
            builder.Services.AddSingleton(filter);
        }

        configureBuilder?.Invoke(builder);
        return builder.Build();
    }

    public static Kernel BuildChatKernel(LlmOptions llmOptions)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpsGuardChatCompletion(llmOptions);
        return builder.Build();
    }
}
