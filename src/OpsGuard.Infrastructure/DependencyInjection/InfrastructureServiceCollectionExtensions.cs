using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OpsGuard.Core.Configuration;
using OpsGuard.Infrastructure.Docker;
using OpsGuard.Infrastructure.Host;
using OpsGuard.Infrastructure.Logging;
using OpsGuard.Infrastructure.Network;
using OpsGuard.Infrastructure.Process;

namespace OpsGuard.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddOpsGuardInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IComposeDockerClient, ComposeDockerClient>();
        services.AddSingleton<IHostMetricsReader, LinuxHostMetricsReader>();
        services.AddSingleton<IHttpEndpointChecker, HttpEndpointChecker>();
        services.AddSingleton<IAutoFunctionInvocationFilter, AgentInvocationFilter>();

        services.AddHttpClient(HttpClientNames.Probe, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.HttpProbeTimeoutSeconds);
        });

        return services;
    }
}
