namespace OpsGuard.Core.Models;

public sealed class ComposeServiceStatusSnapshot
{
    public string ServiceId { get; init; } = string.Empty;

    public string ContainerName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public bool Running { get; init; }

    public int ExitCode { get; init; }

    public int RestartCount { get; init; }

    public string? StartedAt { get; init; }

    public string? FinishedAt { get; init; }

    public string? Error { get; init; }
}
