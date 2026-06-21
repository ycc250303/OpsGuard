namespace OpsGuard.Core.Models;

public sealed class HttpProbeResult
{
    public string ServiceId { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public int? StatusCode { get; init; }

    public double LatencyMs { get; init; }

    public bool Healthy { get; init; }

    public string? Error { get; init; }
}
