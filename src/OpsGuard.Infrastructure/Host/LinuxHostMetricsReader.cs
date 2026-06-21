using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpsGuard.Core.Models;

namespace OpsGuard.Infrastructure.Host;

public sealed class LinuxHostMetricsReader : IHostMetricsReader
{
    private static readonly string[] MemInfoKeys = ["MemTotal:", "MemAvailable:"];
    private readonly ILogger<LinuxHostMetricsReader> _logger;

    public LinuxHostMetricsReader(ILogger<LinuxHostMetricsReader> logger)
    {
        _logger = logger;
    }

    public Task<DiagnosticResult<HostMetricsSnapshot>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Task.FromResult(DiagnosticResult<HostMetricsSnapshot>.Fail(
                "Host metrics are only supported on Linux. Run OpsGuard on the target server."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cpu = ReadCpuUsagePercent();
            var (memTotal, memAvailable) = ReadMemory();
            var (load1, load5, load15) = ReadLoadAverage();
            var disks = ReadDiskUsage();

            var memUsed = memTotal - memAvailable;
            var memUsagePercent = memTotal > 0 ? memUsed * 100.0 / memTotal : 0;

            return Task.FromResult(DiagnosticResult<HostMetricsSnapshot>.Ok(new HostMetricsSnapshot
            {
                CpuUsagePercent = cpu,
                MemoryTotalBytes = memTotal,
                MemoryAvailableBytes = memAvailable,
                MemoryUsagePercent = Math.Round(memUsagePercent, 2),
                LoadAverage1 = load1,
                LoadAverage5 = load5,
                LoadAverage15 = load15,
                Disks = disks
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read host metrics");
            return Task.FromResult(DiagnosticResult<HostMetricsSnapshot>.Fail($"Failed to read host metrics: {ex.Message}"));
        }
    }

    private static double ReadCpuUsagePercent()
    {
        var stat1 = ParseCpuStat("/proc/stat");
        Thread.Sleep(100);
        var stat2 = ParseCpuStat("/proc/stat");

        var idleDelta = stat2.Idle - stat1.Idle;
        var totalDelta = stat2.Total - stat1.Total;
        if (totalDelta <= 0)
        {
            return 0;
        }

        var usage = (1.0 - idleDelta / (double)totalDelta) * 100.0;
        return Math.Round(Math.Clamp(usage, 0, 100), 2);
    }

    private static (long Total, long Available) ReadMemory()
    {
        long total = 0;
        long available = 0;

        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            foreach (var key in MemInfoKeys)
            {
                if (!line.StartsWith(key, StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !long.TryParse(parts[1], out var kb))
                {
                    continue;
                }

                if (key == "MemTotal:")
                {
                    total = kb * 1024;
                }
                else
                {
                    available = kb * 1024;
                }
            }
        }

        return (total, available);
    }

    private static (double Load1, double Load5, double Load15) ReadLoadAverage()
    {
        var line = File.ReadLines("/proc/loadavg").FirstOrDefault() ?? "0 0 0";
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var load1 = parts.Length > 0 ? ParseDouble(parts[0]) : 0;
        var load5 = parts.Length > 1 ? ParseDouble(parts[1]) : 0;
        var load15 = parts.Length > 2 ? ParseDouble(parts[2]) : 0;
        return (load1, load5, load15);
    }

    private static List<DiskUsageSnapshot> ReadDiskUsage()
    {
        var disks = new List<DiskUsageSnapshot>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Network))
            {
                continue;
            }

            var total = drive.TotalSize;
            var free = drive.AvailableFreeSpace;
            var used = total - free;
            var usagePercent = total > 0 ? used * 100.0 / total : 0;

            disks.Add(new DiskUsageSnapshot
            {
                MountPoint = drive.Name,
                TotalBytes = total,
                UsedBytes = used,
                UsagePercent = Math.Round(usagePercent, 2)
            });
        }

        return disks.OrderByDescending(d => d.UsagePercent).ToList();
    }

    private static (long Idle, long Total) ParseCpuStat(string path)
    {
        var cpuLine = File.ReadLines(path).FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Unable to read /proc/stat");

        var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            throw new InvalidOperationException("Unexpected /proc/stat format");
        }

        long total = 0;
        for (var i = 1; i < parts.Length; i++)
        {
            total += long.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        var idle = long.Parse(parts[4], CultureInfo.InvariantCulture);
        return (idle, total);
    }

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
