using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsGuard.Core.Configuration;
using OpsGuard.Core.Models;
using OpsGuard.Infrastructure.Process;

namespace OpsGuard.Infrastructure.Docker;

public sealed class ComposeDockerClient : IComposeDockerClient
{
    private readonly IProcessRunner _processRunner;
    private readonly AgentOptions _options;
    private readonly ILogger<ComposeDockerClient> _logger;

    public ComposeDockerClient(
        IProcessRunner processRunner,
        IOptions<AgentOptions> options,
        ILogger<ComposeDockerClient> logger)
    {
        _processRunner = processRunner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DiagnosticResult<ComposeServiceStatusSnapshot>> InspectAsync(
        ValidatedContainerName containerName,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var args = new[]
        {
            "inspect",
            containerName.Value,
            "--format",
            "{{json .State}}"
        };

        var result = await _processRunner.RunAsync("docker", args, TimeSpan.FromSeconds(30), cancellationToken);
        if (result.ExitCode != 0)
        {
            _logger.LogWarning("docker inspect failed for {Container}: {Error}", containerName.Value, result.StandardError);
            return DiagnosticResult<ComposeServiceStatusSnapshot>.Fail(
                $"docker inspect failed: {TrimError(result.StandardError)}");
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StandardOutput);
            var root = doc.RootElement;
            var status = root.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() ?? "unknown" : "unknown";
            var running = root.TryGetProperty("Running", out var runningProp) && runningProp.GetBoolean();
            var exitCode = root.TryGetProperty("ExitCode", out var exitProp) ? exitProp.GetInt32() : 0;
            var startedAt = root.TryGetProperty("StartedAt", out var startedProp) ? startedProp.GetString() : null;
            var finishedAt = root.TryGetProperty("FinishedAt", out var finishedProp) ? finishedProp.GetString() : null;

            var restartCount = 0;
            if (root.TryGetProperty("RestartCount", out var restartProp))
            {
                restartCount = restartProp.GetInt32();
            }

            return DiagnosticResult<ComposeServiceStatusSnapshot>.Ok(new ComposeServiceStatusSnapshot
            {
                ServiceId = serviceId,
                ContainerName = containerName.Value,
                Status = status,
                Running = running,
                ExitCode = exitCode,
                RestartCount = restartCount,
                StartedAt = startedAt,
                FinishedAt = finishedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse docker inspect output for {Container}", containerName.Value);
            return DiagnosticResult<ComposeServiceStatusSnapshot>.Fail($"Failed to parse docker inspect output: {ex.Message}");
        }
    }

    public async Task<DiagnosticResult<string>> GetLogsAsync(
        ValidatedContainerName containerName,
        string serviceId,
        int tailLines,
        string? since = null,
        CancellationToken cancellationToken = default)
    {
        if (!DockerLogSinceValidator.TryNormalize(since, _options.MaxLogSinceHours, out var normalizedSince, out var sinceError))
        {
            return DiagnosticResult<string>.Fail(sinceError!);
        }

        var clampedTail = Math.Clamp(tailLines, 1, _options.MaxLogTailLines);
        var args = new List<string> { "logs", "--timestamps" };

        if (!string.IsNullOrWhiteSpace(normalizedSince))
        {
            args.Add("--since");
            args.Add(normalizedSince);
        }

        args.Add("--tail");
        args.Add(clampedTail.ToString());
        args.Add(containerName.Value);

        var timeout = string.IsNullOrWhiteSpace(normalizedSince)
            ? TimeSpan.FromSeconds(30)
            : TimeSpan.FromSeconds(60);

        var result = await _processRunner.RunAsync("docker", args, timeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            _logger.LogWarning("docker logs failed for {Container}: {Error}", containerName.Value, result.StandardError);
            return DiagnosticResult<string>.Fail($"docker logs failed: {TrimError(result.StandardError)}");
        }

        var output = string.IsNullOrWhiteSpace(result.StandardOutput) ? result.StandardError : result.StandardOutput;
        return DiagnosticResult<string>.Ok(output.TrimEnd());
    }

    private static string TrimError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "unknown docker error";
        }

        return error.Length <= 500 ? error.Trim() : error[..500].Trim();
    }
}
