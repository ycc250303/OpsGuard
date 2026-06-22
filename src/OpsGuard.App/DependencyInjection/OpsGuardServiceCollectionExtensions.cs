using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsGuard.App.Services;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Memory;
using OpsGuard.Core.Topology;
using OpsGuard.Infrastructure.DependencyInjection;
using OpsGuard.Plugins.DependencyInjection;

namespace OpsGuard.App.DependencyInjection;

public static class OpsGuardServiceCollectionExtensions
{
    public static IServiceCollection AddOpsGuard(
        this IServiceCollection services,
        IConfiguration configuration,
        string topologyPath)
    {
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.Configure<ComposeTopologyOptions>(options => options.TopologyFile = topologyPath);

        var topologyProvider = new ComposeTopologyProvider(topologyPath);
        services.AddSingleton<IComposeTopologyProvider>(topologyProvider);
        services.AddSingleton<IServiceCatalog, ServiceCatalog>();
        services.AddSingleton<ServerContextMemory>();

        services.AddOpsGuardInfrastructure();
        services.AddOpsGuardPlugins();

        services.AddSingleton(sp =>
        {
            var llmOptions = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            var apiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                llmOptions.ApiKey = apiKey;
            }

            return llmOptions;
        });

        services.AddSingleton<OpsGuardOrchestratorFactory>();
        services.AddSingleton<IUserModelSelection, ConfigUserModelSelection>();

        return services;
    }
}
