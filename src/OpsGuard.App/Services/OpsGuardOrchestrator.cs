using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpsGuard.Core.Agents;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Memory;
using OpsGuard.Infrastructure.Llm;
using OpsGuard.Plugins.DependencyInjection;

namespace OpsGuard.App.Services;

public sealed class OpsGuardOrchestrator
{
    private readonly ChatCompletionAgent _collector;
    private readonly ChatCompletionAgent _analyzer;
    private readonly ChatCompletionAgent _advisor;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<OpsGuardOrchestrator> _logger;

    public OpsGuardOrchestrator(
        IServiceProvider services,
        LlmOptions llmOptions,
        ServerContextMemory serverContextMemory,
        IOptions<AgentOptions> agentOptions,
        ILogger<OpsGuardOrchestrator> logger)
    {
        _agentOptions = agentOptions.Value;
        _logger = logger;

        var contextSummary = serverContextMemory.BuildSummary();
        var collectorKernel = services.BuildCollectorKernel(llmOptions);
        collectorKernel.RegisterOpsGuardPlugins(services);

        var chatKernel = LlmKernelBuilderExtensions.BuildChatKernel(llmOptions);

        _collector = CreateAgent(
            "Collector",
            AgentPrompts.CollectorRole,
            contextSummary,
            collectorKernel,
            LlmKernelBuilderExtensions.CreateToolCallingSettings());

        _analyzer = CreateAgent(
            "Analyzer",
            AgentPrompts.AnalyzerRole,
            contextSummary,
            chatKernel,
            LlmKernelBuilderExtensions.CreateChatOnlySettings());

        _advisor = CreateAgent(
            "Advisor",
            AgentPrompts.AdvisorRole,
            contextSummary,
            chatKernel,
            LlmKernelBuilderExtensions.CreateChatOnlySettings());
    }

    public async Task<string> RunAsync(string taskPrompt, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(_agentOptions.OrchestrationTimeoutMinutes));

        try
        {
            _logger.LogInformation("Starting multi-agent pipeline");

            var collectorFacts = await InvokeAgentStageAsync(
                _collector,
                taskPrompt,
                timeoutCts.Token);

            var analyzerInput = $"""
                {taskPrompt}

                ## Collector 采集结果
                {collectorFacts}
                """;

            var analysis = await InvokeAgentStageAsync(
                _analyzer,
                analyzerInput,
                timeoutCts.Token);

            var advisorInput = $"""
                {taskPrompt}

                ## Analyzer 分析结果
                {analysis}
                """;

            var report = await InvokeAgentStageAsync(
                _advisor,
                advisorInput,
                timeoutCts.Token);

            return string.IsNullOrWhiteSpace(report)
                ? "未能生成诊断报告，请重试或缩小问题范围。"
                : report.Trim();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"诊断超时（{_agentOptions.OrchestrationTimeoutMinutes} 分钟），请缩小问题范围后重试。");
        }
    }

    private async Task<string> InvokeAgentStageAsync(
        ChatCompletionAgent agent,
        string input,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {agent.Name} ===");

        var thread = new ChatHistoryAgentThread();
        var response = await agent.InvokeAsync(input, thread, cancellationToken: cancellationToken)
            .FirstAsync(cancellationToken);

        var content = response.Message.Content ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(content))
        {
            Console.WriteLine(content);
        }

        return content;
    }

    private static ChatCompletionAgent CreateAgent(
        string name,
        string rolePrompt,
        string contextSummary,
        Kernel kernel,
        OpenAIPromptExecutionSettings settings)
    {
        return new ChatCompletionAgent
        {
            Name = name,
            Instructions = $"{rolePrompt}\n\n{contextSummary}",
            Kernel = kernel,
            Arguments = new KernelArguments(settings)
        };
    }
}
