using System.ComponentModel;
using Microsoft.SemanticKernel;
using OpsGuard.Core.Serialization;
using OpsGuard.Core.Topology;
using OpsGuard.Infrastructure.Network;

namespace OpsGuard.Plugins.Network;

public sealed class HttpCheckPlugin
{
    private readonly IServiceCatalog _serviceCatalog;
    private readonly IHttpEndpointChecker _httpEndpointChecker;

    public HttpCheckPlugin(IServiceCatalog serviceCatalog, IHttpEndpointChecker httpEndpointChecker)
    {
        _serviceCatalog = serviceCatalog;
        _httpEndpointChecker = httpEndpointChecker;
    }

    [KernelFunction("CheckHttpEndpoint")]
    [Description("对 JSON 拓扑中配置了 HealthUrl 的服务执行 HTTP GET 探活。")]
    public async Task<string> CheckHttpEndpointAsync(
        [Description("拓扑 JSON 中的服务 Id，例如 web-gateway")] string serviceId,
        CancellationToken cancellationToken = default)
    {
        if (!_serviceCatalog.TryGetService(serviceId, out var service) || service is null)
        {
            return DiagnosticJson.Serialize(new
            {
                success = false,
                error = $"Unknown serviceId '{serviceId}'. Call GetComposeTopology first."
            });
        }

        if (string.IsNullOrWhiteSpace(service.HealthUrl))
        {
            return DiagnosticJson.Serialize(new
            {
                success = false,
                error = $"Service '{serviceId}' has no HealthUrl configured."
            });
        }

        var result = await _httpEndpointChecker.ProbeAsync(serviceId, service.HealthUrl, cancellationToken);
        return DiagnosticJson.Serialize(result);
    }
}
