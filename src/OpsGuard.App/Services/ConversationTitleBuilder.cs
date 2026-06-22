using OpsGuard.App.Services.Conversations;

namespace OpsGuard.App.Services;

public static class ConversationTitleBuilder
{
    private const int MaxLength = 40;

    public static string FromUserMessage(string userMessage)
    {
        var normalized = userMessage
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        if (normalized.Length == 0)
        {
            return "新会话";
        }

        return normalized.Length <= MaxLength
            ? normalized
            : normalized[..MaxLength] + "…";
    }
}

public static class ConversationMessageMapper
{
    public static ChatMessage ToChatMessage(StoredMessage message) =>
        new(message.Role, message.Content, message.At);

    public static StoredMessage ToStoredMessage(ChatMessage message) => new()
    {
        Role = message.Role,
        Content = message.Content,
        At = message.At
    };
}
