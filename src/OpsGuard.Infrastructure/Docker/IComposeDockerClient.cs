using OpsGuard.Core.Models;

namespace OpsGuard.Infrastructure.Docker;

public interface IComposeDockerClient
{
    Task<DiagnosticResult<ComposeServiceStatusSnapshot>> InspectAsync(
        ValidatedContainerName containerName,
        string serviceId,
        CancellationToken cancellationToken = default);

    Task<DiagnosticResult<string>> GetLogsAsync(
        ValidatedContainerName containerName,
        string serviceId,
        int tailLines,
        CancellationToken cancellationToken = default);
}
