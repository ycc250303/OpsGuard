using OpsGuard.Core.Models;

namespace OpsGuard.Infrastructure.Host;

public interface IHostMetricsReader
{
    Task<DiagnosticResult<HostMetricsSnapshot>> ReadAsync(CancellationToken cancellationToken = default);
}
