using System.Runtime.InteropServices;

namespace OpsGuard.Core.Memory;

public sealed record OpsGuardRuntimeFacts(
    string MachineName,
    string OsDescription,
    string OsPlatform,
    bool IsLinux,
    bool HostMetricsSupported);

public static class OpsGuardRuntimeContext
{
    public static OpsGuardRuntimeFacts Current()
    {
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
            : "Other";

        var isLinux = platform == "Linux";

        return new OpsGuardRuntimeFacts(
            MachineName: Environment.MachineName,
            OsDescription: RuntimeInformation.OSDescription,
            OsPlatform: platform,
            IsLinux: isLinux,
            HostMetricsSupported: isLinux);
    }
}
