namespace OpsGuard.App.Services.Conversations;

/// <summary>CLI 等无持久化场景的空实现。</summary>
public sealed class NullConversationStore : IConversationStore
{
    public bool IsEnabled => false;

    public string? ActiveSessionId => null;

    public IReadOnlyList<ConversationSummary> Sessions => [];

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<StoredConversation> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("当前环境未启用会话持久化。");

    public Task<StoredConversation> CreateSessionAsync(string modelId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("当前环境未启用会话持久化。");

    public Task SetActiveSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SaveSessionAsync(StoredConversation session, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
