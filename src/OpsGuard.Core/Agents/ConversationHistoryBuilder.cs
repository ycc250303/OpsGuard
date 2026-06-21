using Microsoft.SemanticKernel.ChatCompletion;

namespace OpsGuard.Core.Agents;

public static class ConversationHistoryBuilder
{
    public static string BuildRecentHistorySummary(ChatHistory chatHistory, int maxTurns)
    {
        if (maxTurns <= 0 || chatHistory.Count == 0)
        {
            return string.Empty;
        }

        var recent = chatHistory.TakeLast(maxTurns * 2).ToList();
        var lines = recent.Select(m => $"- {m.Role}: {TrimContent(m.Content)}");
        return "## 最近对话\n" + string.Join(Environment.NewLine, lines);
    }

    private static string TrimContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 300 ? normalized : normalized[..300] + "...";
    }
}
