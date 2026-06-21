using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using OpsGuard.Core.Agents;
using OpsGuard.Core.Configuration;

namespace OpsGuard.App.Services;

public sealed class DiagnosticSessionService
{
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

    public async Task<string> AskAsync(string userInput, CancellationToken cancellationToken = default)
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

            var report = await _orchestrator.RunAsync(taskPrompt, cancellationToken);
            _chatHistory.AddAssistantMessage(report);
            _messages.Add(ChatMessage.Assistant(report));
            return report;
        }
        catch (Exception ex)
        {
            var error = $"诊断失败: {ex.Message}";
            _chatHistory.AddAssistantMessage(error);
            _messages.Add(ChatMessage.Assistant(error));
            return error;
        }
        finally
        {
            IsRunning = false;
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
}

public sealed record ChatMessage(string Role, string Content, DateTimeOffset At)
{
    public static ChatMessage User(string content) => new("user", content, DateTimeOffset.Now);

    public static ChatMessage Assistant(string content) => new("assistant", content, DateTimeOffset.Now);

    public bool IsUser => Role == "user";
}
