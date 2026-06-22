using System.Runtime.CompilerServices;
using System.Text;
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

        _logger.LogInformation("Starting multi-agent pipeline (streaming)");

        yield return new DiagnosticChunk("Collector", DiagnosticChunkPhase.Started, null);
        var collectorFacts = await InvokeAgentStageAsync(_collector, taskPrompt, timeoutCts.Token);
        yield return new DiagnosticChunk("Collector", DiagnosticChunkPhase.Completed, collectorFacts);

        var analyzerInput = $"""
            {taskPrompt}

            ## Collector 采集结果
            {collectorFacts}
            """;

        yield return new DiagnosticChunk("Analyzer", DiagnosticChunkPhase.Started, null);
        var analysis = await InvokeAgentStageAsync(_analyzer, analyzerInput, timeoutCts.Token);
        yield return new DiagnosticChunk("Analyzer", DiagnosticChunkPhase.Completed, analysis);

        var advisorInput = $"""
            {taskPrompt}

            ## Analyzer 分析结果
            {analysis}
            """;

        yield return new DiagnosticChunk("Advisor", DiagnosticChunkPhase.Started, null);
        var report = await InvokeAgentStageAsync(_advisor, advisorInput, timeoutCts.Token);
        yield return new DiagnosticChunk("Advisor", DiagnosticChunkPhase.Completed, report);
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

    internal static string BuildStreamingMarkdown(IReadOnlyList<DiagnosticChunk> chunks)
    {
        var sb = new StringBuilder();
        var byStage = chunks
            .GroupBy(c => c.Stage)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var stage in new[] { "Collector", "Analyzer", "Advisor" })
        {
            if (!byStage.TryGetValue(stage, out var stageChunks))
            {
                continue;
            }

            var started = stageChunks.Any(c => c.Phase == DiagnosticChunkPhase.Started);
            if (!started)
            {
                continue;
            }

            var completed = stageChunks.LastOrDefault(c => c.Phase == DiagnosticChunkPhase.Completed);

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine($"## {stage}");
            sb.AppendLine();

            if (completed is not null)
            {
                sb.AppendLine(string.IsNullOrWhiteSpace(completed.Content)
                    ? "_（无输出）_"
                    : completed.Content.Trim());
            }
            else
            {
                sb.AppendLine("*运行中…*");
            }
        }

        return sb.ToString().Trim();
    }
}
