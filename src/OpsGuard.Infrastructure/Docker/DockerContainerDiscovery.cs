using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpsGuard.Core.Models;
using OpsGuard.Core.Topology;
using OpsGuard.Infrastructure.Process;

namespace OpsGuard.Infrastructure.Docker;

public sealed class DockerContainerDiscovery : IDockerContainerDiscovery
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<DockerContainerDiscovery> _logger;

    public DockerContainerDiscovery(IProcessRunner processRunner, ILogger<DockerContainerDiscovery> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredContainer>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            "docker",
            ["ps", "-a", "--format", "{{json .}}"],
            TimeSpan.FromSeconds(30),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("docker ps failed: {Error}", result.StandardError);
            return [];
        }

        return ParseLines(result.StandardOutput);
    }

    public static IReadOnlyList<DiscoveredContainer> ParseLines(string output)
    {
        var containers = new List<DiscoveredContainer>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var names = GetString(root, "Names");
                if (string.IsNullOrWhiteSpace(names))
                {
                    continue;
                }

                var containerName = names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]
                    .TrimStart('/');

                var labels = ParseLabels(GetString(root, "Labels"));
                labels.TryGetValue("com.docker.compose.project", out var composeProject);
                labels.TryGetValue("com.docker.compose.service", out var composeService);

                containers.Add(new DiscoveredContainer
                {
                    ContainerName = containerName,
                    ComposeProject = composeProject,
                    ComposeService = composeService,
                    State = GetString(root, "State") ?? "unknown",
                    Status = GetString(root, "Status") ?? string.Empty,
                    Ports = GetString(root, "Ports"),
                    Image = GetString(root, "Image")
                });
            }
            catch (JsonException)
            {
                // skip malformed line
            }
        }

        return containers;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;

    internal static Dictionary<string, string> ParseLabels(string? raw)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return labels;
        }

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            labels[part[..separator].Trim()] = part[(separator + 1)..].Trim();
        }

        return labels;
    }
}
