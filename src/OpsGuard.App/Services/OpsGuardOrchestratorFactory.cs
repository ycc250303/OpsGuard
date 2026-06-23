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

    public OpsGuardOrchestrator Create(string modelId) =>
        ActivatorUtilities.CreateInstance<OpsGuardOrchestrator>(
            _services,
            LlmModelCatalog.ResolveOptions(modelId, _baseOptions));
}
