namespace OpsGuard.Core.Models;

public sealed class HostMetricsSnapshot
{
    public double CpuUsagePercent { get; init; }

    public long MemoryTotalBytes { get; init; }

    public long MemoryAvailableBytes { get; init; }

    public double MemoryUsagePercent { get; init; }

    public double LoadAverage1 { get; init; }

    public double LoadAverage5 { get; init; }

    public double LoadAverage15 { get; init; }

    public List<DiskUsageSnapshot> Disks { get; init; } = [];
}

public sealed class DiskUsageSnapshot
{
    public string MountPoint { get; init; } = string.Empty;

    public long TotalBytes { get; init; }

    public long UsedBytes { get; init; }

    public double UsagePercent { get; init; }
}
