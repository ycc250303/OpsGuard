namespace OpsGuard.Core.Streaming;

public enum DiagnosticChunkPhase
{
    Started,
    Delta,
    ToolInvoking,
    ToolCompleted,
    Completed
}

public sealed record DiagnosticChunk(
    string Stage,
    DiagnosticChunkPhase Phase,
    string? Content,
    string? ToolName = null);
