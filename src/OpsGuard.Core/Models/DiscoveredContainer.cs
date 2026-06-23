namespace OpsGuard.Core.Models;

public sealed class DiscoveredContainer
{
    public required string ContainerName { get; init; }

    public string? ComposeProject { get; init; }

    public string? ComposeService { get; init; }

    public required string State { get; init; }

    public required string Status { get; init; }

    public string? Ports { get; init; }

    public string? Image { get; init; }
}
