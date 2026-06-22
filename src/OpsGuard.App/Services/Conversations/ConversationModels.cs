namespace OpsGuard.App.Services.Conversations;

public sealed class ConversationSummary
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StoredConversation
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<StoredMessage> Messages { get; set; } = [];
}

public sealed class StoredMessage
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }
}
