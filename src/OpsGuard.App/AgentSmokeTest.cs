using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpsGuard.Core.Configuration;
using OpsGuard.Infrastructure.Llm;
using OpsGuard.Plugins.Host;

namespace OpsGuard.App;

internal static class AgentSmokeTest
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var llmOptions = services.GetRequiredService<LlmOptions>();
        if (string.IsNullOrWhiteSpace(llmOptions.ApiKey)
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY")))
        {
            Console.WriteLine("[FAIL] DASHSCOPE_API_KEY 未配置");
            return 1;
        }

        Console.WriteLine("[OK] API Key 已加载");

        var hostPlugin = services.GetRequiredService<HostMetricsPlugin>();
        var hostJson = await hostPlugin.GetHostMetricsAsync();
        if (hostJson.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase)
            || hostJson.Contains("\"Success\":true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[OK] GetHostMetrics Plugin");
        }
        else
        {
            Console.WriteLine($"[WARN] GetHostMetrics: {hostJson[..Math.Min(200, hostJson.Length)]}");
        }

        var chatKernel = LlmKernelBuilderExtensions.BuildChatKernel(llmOptions);
        var chat = chatKernel.GetRequiredService<IChatCompletionService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var reply = await chat.GetChatMessageContentAsync(
            "只回复 OK 两个字母",
            new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.None() },
            chatKernel,
            cts.Token);
        Console.WriteLine($"[OK] LLM 连通: {reply.Content?.Trim()}");

        var collectorKernel = services.BuildCollectorKernel(llmOptions);
        OpsGuard.Plugins.DependencyInjection.PluginServiceCollectionExtensions.RegisterOpsGuardPlugins(
            collectorKernel,
            services);

        var agent = new Microsoft.SemanticKernel.Agents.ChatCompletionAgent
        {
            Name = "CollectorSmoke",
            Instructions = "你是采集器。用户问 backend 状态时，调用 GetComposeServiceStatus 工具，然后简短汇报。",
            Kernel = collectorKernel,
            Arguments = new KernelArguments(LlmKernelBuilderExtensions.CreateToolCallingSettings())
        };

        var thread = new Microsoft.SemanticKernel.Agents.ChatHistoryAgentThread();
        using var agentCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var response = await agent.InvokeAsync("backend 容器运行正常吗？", thread, cancellationToken: agentCts.Token)
            .FirstAsync(agentCts.Token);

        Console.WriteLine($"[OK] Collector Agent + Tool: {response.Message.Content?[..Math.Min(300, response.Message.Content.Length)]}");

        return 0;
    }
}
