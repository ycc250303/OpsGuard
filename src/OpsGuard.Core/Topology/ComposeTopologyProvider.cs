using System.Text.Json;
using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

public sealed class ComposeTopologyProvider : IComposeTopologyProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ComposeTopologyProvider(string topologyFilePath)
    {
        if (string.IsNullOrWhiteSpace(topologyFilePath))
        {
            throw new TopologyLoadException("Topology file path is required.");
        }

        if (!File.Exists(topologyFilePath))
        {
            throw new TopologyLoadException($"Topology file not found: {topologyFilePath}");
        }

        try
        {
            var json = File.ReadAllText(topologyFilePath);
            var topology = JsonSerializer.Deserialize<ComposeTopology>(json, JsonOptions)
                ?? throw new TopologyLoadException("Topology JSON deserialized to null.");

            Validate(topology);
            Topology = topology;
        }
        catch (TopologyLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TopologyLoadException($"Failed to load topology from {topologyFilePath}.", ex);
        }
    }

    public ComposeTopology Topology { get; }

    internal static void Validate(ComposeTopology topology)
    {
        if (string.IsNullOrWhiteSpace(topology.ComposeProjectName))
        {
            throw new TopologyLoadException("ComposeProjectName is required.");
        }

        if (topology.Services.Count == 0)
        {
            throw new TopologyLoadException("At least one service must be defined in topology.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in topology.Services)
        {
            if (string.IsNullOrWhiteSpace(service.Id))
            {
                throw new TopologyLoadException("Each service must have a non-empty Id.");
            }

            if (!ids.Add(service.Id))
            {
                throw new TopologyLoadException($"Duplicate service Id: {service.Id}");
            }

            if (string.IsNullOrWhiteSpace(service.ContainerName))
            {
                throw new TopologyLoadException($"Service '{service.Id}' must have ContainerName.");
            }
        }
    }
}
