using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using OpsGuard.Core.Agents;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Streaming;
using OpsGuard.Infrastructure.Streaming;

namespace OpsGuard.App.Services;

public sealed class DiagnosticSessionService
{
    private const int StreamNotifyIntervalMs = 80;

    private readonly OpsGuardOrchestrator _orchestrator;
    private readonly AgentOptions _agentOptions;
    private readonly ChatHistory _chatHistory = new();
    private readonly ChatHistoryAgentThread _agentThread;

    public DiagnosticSessionService(
        OpsGuardOrchestrator orchestrator,
        IOptions<AgentOptions> agentOptions)
    {
        _orchestrator = orchestrator;
        _agentOptions = agentOptions.Value;
        _agentThread = new ChatHistoryAgentThread(_chatHistory);
    }

    private readonly List<ChatMessage> _messages = [];

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public bool IsRunning { get; private set; }

    public Task<string> AskAsync(string userInput, CancellationToken cancellationToken = default) =>
        AskStreamingAsync(userInput, onUpdated: null, cancellationToken);

    public async Task<string> AskStreamingAsync(
        string userInput,
        Action? onUpdated = null,
        CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("上一条诊断尚未完成，请稍候。");
        }

        userInput = userInput.Trim();
        if (userInput.Length == 0)
        {
            throw new ArgumentException("请输入问题。", nameof(userInput));
        }

        IsRunning = true;
        _messages.Add(ChatMessage.User(userInput));
        var assistantIndex = _messages.Count;
        _messages.Add(ChatMessage.Assistant(string.Empty));
        Notify(onUpdated);

        var streamBuilder = new DiagnosticStreamBuilder();
        var advisorReport = string.Empty;
        var lastNotifyAt = DateTime.UtcNow;

        void ApplyChunk(DiagnosticChunk chunk)
        {
            streamBuilder.Apply(chunk);

            if (chunk.Stage == "Advisor"
                && chunk.Phase == DiagnosticChunkPhase.Completed
                && !string.IsNullOrWhiteSpace(chunk.Content))
            {
                advisorReport = chunk.Content.Trim();
            }

            var now = DateTime.UtcNow;
            var shouldNotify = chunk.Phase is not DiagnosticChunkPhase.Delta
                || (now - lastNotifyAt).TotalMilliseconds >= StreamNotifyIntervalMs;

            if (!shouldNotify)
            {
                return;
            }

            lastNotifyAt = now;
            _messages[assistantIndex] = ChatMessage.Assistant(streamBuilder.BuildMarkdown(streaming: true));
            Notify(onUpdated);
        }

        try
        {
            _chatHistory.AddUserMessage(userInput);
            _ = _agentThread;

            var historySummary = ConversationHistoryBuilder.BuildRecentHistorySummary(
                _chatHistory,
                _agentOptions.ConversationHistoryTurns);

            var taskPrompt = string.IsNullOrWhiteSpace(historySummary)
                ? $"## 用户问题\n{userInput}"
                : $"{historySummary}\n\n## 用户问题\n{userInput}";

            DiagnosticStreamContext.SetNotifier(ApplyChunk);
            try
            {
                await foreach (var chunk in _orchestrator.RunStreamingAsync(taskPrompt, cancellationToken))
                {
                    ApplyChunk(chunk);
                }
            }
            finally
            {
                DiagnosticStreamContext.SetNotifier(null);
            }

            _messages[assistantIndex] = ChatMessage.Assistant(streamBuilder.BuildMarkdown(streaming: false));
            Notify(onUpdated);

            var finalContent = _messages[assistantIndex].Content;
            _chatHistory.AddAssistantMessage(finalContent);

            return string.IsNullOrWhiteSpace(advisorReport)
                ? "未能生成诊断报告，请重试或缩小问题范围。"
                : advisorReport;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var error = $"诊断超时（{_agentOptions.OrchestrationTimeoutMinutes} 分钟），请缩小问题范围后重试。";
            _messages[assistantIndex] = ChatMessage.Assistant(error);
            _chatHistory.AddAssistantMessage(error);
            Notify(onUpdated);
            return error;
        }
        catch (Exception ex)
        {
            var error = $"诊断失败: {ex.Message}";
            _messages[assistantIndex] = ChatMessage.Assistant(error);
            _chatHistory.AddAssistantMessage(error);
            Notify(onUpdated);
            return error;
        }
        finally
        {
            IsRunning = false;
            DiagnosticStreamContext.SetNotifier(null);
            DiagnosticStreamContext.SetCurrentStage(null);
            Notify(onUpdated);
        }
    }

    public void Clear()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("诊断进行中，无法清空。");
        }

        _messages.Clear();
        _chatHistory.Clear();
    }

    private static void Notify(Action? onUpdated) => onUpdated?.Invoke();
}

public sealed record ChatMessage(string Role, string Content, DateTimeOffset At)
{
    public static ChatMessage User(string content) => new("user", content, DateTimeOffset.Now);

    public static ChatMessage Assistant(string content) => new("assistant", content, DateTimeOffset.Now);

    public bool IsUser => Role == "user";
}
