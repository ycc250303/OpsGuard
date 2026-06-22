using Microsoft.Extensions.DependencyInjection;
using OpsGuard.Core.Configuration;

namespace OpsGuard.App.Services;

public sealed class OpsGuardOrchestratorFactory
{
    private readonly IServiceProvider _services;
    private readonly LlmOptions _baseOptions;

    public OpsGuardOrchestratorFactory(IServiceProvider services, LlmOptions baseOptions)
    {
        _services = services;
        _baseOptions = baseOptions;
    }

    public OpsGuardOrchestrator Create(string modelId)
    {
        var options = new LlmOptions
        {
            ModelId = LlmModelCatalog.NormalizeOrThrow(modelId),
            Endpoint = _baseOptions.Endpoint,
            ApiKey = _baseOptions.ApiKey
        };

        return ActivatorUtilities.CreateInstance<OpsGuardOrchestrator>(_services, options);
    }
}
