using OpsGuard.Core.Streaming;

namespace OpsGuard.Infrastructure.Streaming;

public static class DiagnosticStreamContext
{
    private static readonly AsyncLocal<string?> CurrentStage = new();
    private static readonly AsyncLocal<Action<DiagnosticChunk>?> Notifier = new();

    public static void SetCurrentStage(string? stage) => CurrentStage.Value = stage;

    public static void SetNotifier(Action<DiagnosticChunk>? notifier) => Notifier.Value = notifier;

    public static void PublishToolInvoking(string toolName)
    {
        Publish(new DiagnosticChunk(
            CurrentStage.Value ?? "Collector",
            DiagnosticChunkPhase.ToolInvoking,
            null,
            toolName));
    }

    public static void PublishToolCompleted(string toolName)
    {
        Publish(new DiagnosticChunk(
            CurrentStage.Value ?? "Collector",
            DiagnosticChunkPhase.ToolCompleted,
            null,
            toolName));
    }

    private static void Publish(DiagnosticChunk chunk) => Notifier.Value?.Invoke(chunk);
}
