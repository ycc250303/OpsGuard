namespace OpsGuard.App.Services.Conversations;

public interface IConversationStore
{
    bool IsEnabled { get; }

    string? ActiveSessionId { get; }

    IReadOnlyList<ConversationSummary> Sessions { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<StoredConversation> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<StoredConversation> CreateSessionAsync(string modelId, CancellationToken cancellationToken = default);

    Task SetActiveSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task SaveSessionAsync(StoredConversation session, CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
