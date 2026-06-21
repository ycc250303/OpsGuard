using OpsGuard.Core.Models;

namespace OpsGuard.Infrastructure.Network;

public interface IHttpEndpointChecker
{
    Task<DiagnosticResult<HttpProbeResult>> ProbeAsync(
        string serviceId,
        string healthUrl,
        CancellationToken cancellationToken = default);
}
