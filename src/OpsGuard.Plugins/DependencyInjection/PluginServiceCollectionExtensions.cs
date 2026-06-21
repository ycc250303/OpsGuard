using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OpsGuard.Plugins.Compose;
using OpsGuard.Plugins.Host;
using OpsGuard.Plugins.Network;

namespace OpsGuard.Plugins.DependencyInjection;

public static class PluginServiceCollectionExtensions
{
    public static Kernel RegisterOpsGuardPlugins(this Kernel kernel, IServiceProvider services)
    {
        kernel.Plugins.AddFromObject(services.GetRequiredService<HostMetricsPlugin>(), "Host");
        kernel.Plugins.AddFromObject(services.GetRequiredService<ComposeTopologyPlugin>(), "ComposeTopology");
        kernel.Plugins.AddFromObject(services.GetRequiredService<ComposeStatusPlugin>(), "ComposeStatus");
        kernel.Plugins.AddFromObject(services.GetRequiredService<ComposeLogsPlugin>(), "ComposeLogs");
        kernel.Plugins.AddFromObject(services.GetRequiredService<HttpCheckPlugin>(), "Network");
        return kernel;
    }

    public static IServiceCollection AddOpsGuardPlugins(this IServiceCollection services)
    {
        services.AddSingleton<HostMetricsPlugin>();
        services.AddSingleton<ComposeTopologyPlugin>();
        services.AddSingleton<ComposeStatusPlugin>();
        services.AddSingleton<ComposeLogsPlugin>();
        services.AddSingleton<HttpCheckPlugin>();
        return services;
    }
}
