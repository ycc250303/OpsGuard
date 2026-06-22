using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpsGuard.Core.Agents;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Streaming;
using OpsGuard.Infrastructure.Streaming;
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
        var report = string.Empty;
        await foreach (var chunk in RunStreamingAsync(taskPrompt, cancellationToken))
        {
            if (chunk.Stage == "Advisor"
                && chunk.Phase == DiagnosticChunkPhase.Completed
                && !string.IsNullOrWhiteSpace(chunk.Content))
            {
                report = chunk.Content;
            }
        }

        return string.IsNullOrWhiteSpace(report)
            ? "未能生成诊断报告，请重试或缩小问题范围。"
            : report.Trim();
    }

    public async IAsyncEnumerable<DiagnosticChunk> RunStreamingAsync(
        string taskPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(_agentOptions.OrchestrationTimeoutMinutes));

        _logger.LogInformation("Starting multi-agent pipeline (token streaming)");

        var collectorFacts = string.Empty;
        await foreach (var chunk in InvokeAgentStageStreamingAsync(
            _collector, "Collector", taskPrompt, timeoutCts.Token))
        {
            if (chunk.Phase == DiagnosticChunkPhase.Completed)
            {
                collectorFacts = chunk.Content ?? string.Empty;
            }

            yield return chunk;
        }

        var analyzerInput = $"""
            {taskPrompt}

            ## Collector 采集结果
            {collectorFacts}
            """;

        var analysis = string.Empty;
        await foreach (var chunk in InvokeAgentStageStreamingAsync(
            _analyzer, "Analyzer", analyzerInput, timeoutCts.Token))
        {
            if (chunk.Phase == DiagnosticChunkPhase.Completed)
            {
                analysis = chunk.Content ?? string.Empty;
            }

            yield return chunk;
        }

        var advisorInput = $"""
            {taskPrompt}

            ## Analyzer 分析结果
            {analysis}
            """;

        await foreach (var chunk in InvokeAgentStageStreamingAsync(
            _advisor, "Advisor", advisorInput, timeoutCts.Token))
        {
            yield return chunk;
        }
    }

    private static async IAsyncEnumerable<DiagnosticChunk> InvokeAgentStageStreamingAsync(
        ChatCompletionAgent agent,
        string stage,
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {agent.Name} ===");

        yield return new DiagnosticChunk(stage, DiagnosticChunkPhase.Started, null);

        DiagnosticStreamContext.SetCurrentStage(stage);

        var thread = new ChatHistoryAgentThread();
        var fullContent = new StringBuilder();

        try
        {
            await foreach (var update in agent.InvokeStreamingAsync(input, thread, cancellationToken: cancellationToken))
            {
                var delta = update.Message?.Content;
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }

                fullContent.Append(delta);
                Console.Write(delta);

                yield return new DiagnosticChunk(stage, DiagnosticChunkPhase.Delta, delta);
            }
        }
        finally
        {
            DiagnosticStreamContext.SetCurrentStage(null);
        }

        Console.WriteLine();
        yield return new DiagnosticChunk(stage, DiagnosticChunkPhase.Completed, fullContent.ToString());
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
