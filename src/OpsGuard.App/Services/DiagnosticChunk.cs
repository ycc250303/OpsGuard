namespace OpsGuard.App.Services;

public enum DiagnosticChunkPhase
{
    Started,
    Completed
}

public sealed record DiagnosticChunk(
    string Stage,
    DiagnosticChunkPhase Phase,
    string? Content);
