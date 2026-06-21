namespace OpsGuard.Core.Models;

public sealed class ComposeServiceDefinition
{
    public string Id { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? ComposeService { get; set; }

    public string? HealthUrl { get; set; }

    public string? Description { get; set; }
}
