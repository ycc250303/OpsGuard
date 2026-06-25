using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using OpsGuard.Core.Agents;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Streaming;
using OpsGuard.App.Services.Conversations;
using OpsGuard.Infrastructure.Streaming;

namespace OpsGuard.App.Services;

public sealed class DiagnosticSessionService
{
    private readonly OpsGuardOrchestratorFactory _orchestratorFactory;
    private readonly IUserModelSelection _modelSelection;
    private readonly IConversationStore _conversationStore;
    private readonly AgentOptions _agentOptions;
    private readonly ChatHistory _chatHistory = new();
    private readonly ChatHistoryAgentThread _agentThread;
    private OpsGuardOrchestrator? _orchestrator;
    private string? _orchestratorModelId;
    private StoredConversation? _activeConversation;
    private bool _initialized;

    public DiagnosticSessionService(
        OpsGuardOrchestratorFactory orchestratorFactory,
        IUserModelSelection modelSelection,
        IConversationStore conversationStore,
        IOptions<AgentOptions> agentOptions)
    {
        _orchestratorFactory = orchestratorFactory;
        _modelSelection = modelSelection;
        _conversationStore = conversationStore;
        _agentOptions = agentOptions.Value;
        _agentThread = new ChatHistoryAgentThread(_chatHistory);
    }

    private readonly List<ChatMessage> _messages = [];

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public bool IsRunning { get; private set; }

    public bool PersistenceEnabled => _conversationStore.IsEnabled;

    public string? ActiveSessionId => _activeConversation?.Id;

    public IReadOnlyList<ConversationSummary> SessionSummaries => _conversationStore.Sessions;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        if (!_conversationStore.IsEnabled)
        {
            _initialized = true;
            return;
        }

        await _conversationStore.InitializeAsync(cancellationToken);
        var activeSessionId = _conversationStore.ActiveSessionId
            ?? throw new InvalidOperationException("未找到活动会话。");

        await LoadSessionAsync(activeSessionId, cancellationToken);
        _initialized = true;
    }

    public async Task CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotRunning();
        await EnsureInitializedAsync(cancellationToken);

        if (!_conversationStore.IsEnabled)
        {
            ClearInMemory();
            return;
        }

        var session = await _conversationStore.CreateSessionAsync(_modelSelection.ModelId, cancellationToken);
        ApplySession(session);
    }

    public async Task SwitchSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        EnsureNotRunning();
        await EnsureInitializedAsync(cancellationToken);

        if (!_conversationStore.IsEnabled)
        {
            return;
        }

        if (string.Equals(_activeConversation?.Id, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        await _conversationStore.SetActiveSessionAsync(sessionId, cancellationToken);
        await LoadSessionAsync(sessionId, cancellationToken);
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        EnsureNotRunning();
        await EnsureInitializedAsync(cancellationToken);

        if (!_conversationStore.IsEnabled)
        {
            ClearInMemory();
            return;
        }

        await _conversationStore.DeleteSessionAsync(sessionId, cancellationToken);

        var nextSessionId = _conversationStore.ActiveSessionId;
        if (string.IsNullOrWhiteSpace(nextSessionId))
        {
            ClearInMemory();
            _activeConversation = null;
            return;
        }

        await LoadSessionAsync(nextSessionId, cancellationToken);
    }

    public Task<string> AskAsync(string userInput, CancellationToken cancellationToken = default) =>
        AskStreamingAsync(userInput, onUpdated: null, cancellationToken);

    public async Task<string> AskStreamingAsync(
        string userInput,
        Func<Task>? onUpdated = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

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
        await NotifyAsync(onUpdated);

        var streamBuilder = new DiagnosticStreamBuilder();
        var advisorReport = string.Empty;
        var lastNotifyAt = DateTime.UtcNow;
        var lastNotifiedMarkdown = string.Empty;

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
                || (now - lastNotifyAt).TotalMilliseconds >= _agentOptions.StreamUiNotifyIntervalMs;

            if (!shouldNotify)
            {
                return;
            }

            var markdown = streamBuilder.BuildMarkdown(streaming: true);
            if (markdown == lastNotifiedMarkdown)
            {
                return;
            }

            lastNotifyAt = now;
            lastNotifiedMarkdown = markdown;
            _messages[assistantIndex] = ChatMessage.Assistant(markdown);
            ScheduleNotify(onUpdated);
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
                await foreach (var chunk in ResolveOrchestrator().RunStreamingAsync(taskPrompt, cancellationToken))
                {
                    ApplyChunk(chunk);
                }
            }
            finally
            {
                DiagnosticStreamContext.SetNotifier(null);
            }

            _messages[assistantIndex] = ChatMessage.Assistant(streamBuilder.BuildMarkdown(streaming: false));
            await NotifyAsync(onUpdated);

            var finalContent = _messages[assistantIndex].Content;
            _chatHistory.AddAssistantMessage(finalContent);

            await PersistCurrentSessionAsync(userInput, cancellationToken);

            return string.IsNullOrWhiteSpace(advisorReport)
                ? "未能生成诊断报告，请重试或缩小问题范围。"
                : advisorReport;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var error = $"诊断超时（{_agentOptions.OrchestrationTimeoutMinutes} 分钟），请缩小问题范围后重试。";
            _messages[assistantIndex] = ChatMessage.Assistant(error);
            _chatHistory.AddAssistantMessage(error);
            await NotifyAsync(onUpdated);
            await PersistCurrentSessionAsync(userInput, cancellationToken);
            return error;
        }
        catch (Exception ex)
        {
            var error = $"诊断失败: {ex.Message}";
            _messages[assistantIndex] = ChatMessage.Assistant(error);
            _chatHistory.AddAssistantMessage(error);
            await NotifyAsync(onUpdated);
            await PersistCurrentSessionAsync(userInput, cancellationToken);
            return error;
        }
        finally
        {
            IsRunning = false;
            DiagnosticStreamContext.SetNotifier(null);
            DiagnosticStreamContext.SetCurrentStage(null);
            await NotifyAsync(onUpdated);
        }
    }

    public void Clear()
    {
        EnsureNotRunning();
        ClearInMemory();
    }

    private async Task PersistCurrentSessionAsync(string latestUserMessage, CancellationToken cancellationToken)
    {
        if (!_conversationStore.IsEnabled || _activeConversation is null)
        {
            return;
        }

        _activeConversation.ModelId = _modelSelection.ModelId;
        if (_activeConversation.Title is "新会话")
        {
            _activeConversation.Title = ConversationTitleBuilder.FromUserMessage(latestUserMessage);
        }

        _activeConversation.Messages.Clear();
        foreach (var message in _messages)
        {
            _activeConversation.Messages.Add(ConversationMessageMapper.ToStoredMessage(message));
        }

        await _conversationStore.SaveSessionAsync(_activeConversation, cancellationToken);
    }

    private async Task LoadSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await _conversationStore.LoadSessionAsync(sessionId, cancellationToken);
        ApplySession(session);
    }

    private void ApplySession(StoredConversation session)
    {
        _activeConversation = session;
        _modelSelection.SetModelId(session.ModelId);

        _messages.Clear();
        foreach (var message in session.Messages)
        {
            _messages.Add(ConversationMessageMapper.ToChatMessage(message));
        }

        RestoreChatHistory(_messages);
        ResetOrchestratorCache();
    }

    private void RestoreChatHistory(IEnumerable<ChatMessage> messages)
    {
        _chatHistory.Clear();
        foreach (var message in messages)
        {
            if (message.IsUser)
            {
                _chatHistory.AddUserMessage(message.Content);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                _chatHistory.AddAssistantMessage(message.Content);
            }
        }
    }

    private void ClearInMemory()
    {
        _messages.Clear();
        _chatHistory.Clear();
        _activeConversation = null;
        ResetOrchestratorCache();
    }

    private void ResetOrchestratorCache()
    {
        _orchestrator = null;
        _orchestratorModelId = null;
    }

    private void EnsureNotRunning()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("诊断进行中，请稍候。");
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    private static Task NotifyAsync(Func<Task>? onUpdated) =>
        onUpdated?.Invoke() ?? Task.CompletedTask;

    private static void ScheduleNotify(Func<Task>? onUpdated)
    {
        if (onUpdated is null)
        {
            return;
        }

        _ = onUpdated.Invoke();
    }

    private OpsGuardOrchestrator ResolveOrchestrator()
    {
        var modelId = _modelSelection.ModelId;
        if (_orchestrator is null || !string.Equals(_orchestratorModelId, modelId, StringComparison.Ordinal))
        {
            _orchestrator = _orchestratorFactory.Create(modelId);
            _orchestratorModelId = modelId;
        }

        return _orchestrator;
    }
}

public sealed record ChatMessage(string Role, string Content, DateTimeOffset At)
{
    public static ChatMessage User(string content) => new("user", content, DateTimeOffset.Now);

    public static ChatMessage Assistant(string content) => new("assistant", content, DateTimeOffset.Now);

    public bool IsUser => Role == "user";
}
