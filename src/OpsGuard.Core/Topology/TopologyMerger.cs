using OpsGuard.Core.Configuration;
using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

public static class TopologyMerger
{
    public static ComposeTopology Merge(
        IReadOnlyList<DiscoveredContainer> discovered,
        ComposeTopology overlay,
        AgentOptions options)
    {
        var filtered = FilterDiscovered(discovered, overlay, options);
        var overlayEntries = overlay.Services;

        var mergedServices = new List<ComposeServiceDefinition>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var container in filtered)
        {
            var overlayMatch = FindOverlayMatch(container, overlayEntries);
            var serviceId = ResolveServiceId(container, overlayMatch, usedIds);

            mergedServices.Add(new ComposeServiceDefinition
            {
                Id = serviceId,
                ContainerName = container.ContainerName,
                DisplayName = overlayMatch?.DisplayName ?? container.ComposeService ?? container.ContainerName,
                ComposeService = container.ComposeService ?? overlayMatch?.ComposeService,
                HealthUrl = overlayMatch?.HealthUrl,
                Description = overlayMatch?.Description ?? BuildAutoDescription(container)
            });
        }

        return new ComposeTopology
        {
            ComposeProjectName = ResolveProjectName(filtered, overlay),
            Host = overlay.Host,
            Services = mergedServices
        };
    }

    private static IReadOnlyList<DiscoveredContainer> FilterDiscovered(
        IReadOnlyList<DiscoveredContainer> discovered,
        ComposeTopology overlay,
        AgentOptions options)
    {
        IEnumerable<DiscoveredContainer> query = discovered;

        if (!string.IsNullOrWhiteSpace(overlay.ComposeProjectName))
        {
            query = query.Where(container => string.Equals(
                container.ComposeProject,
                overlay.ComposeProjectName,
                StringComparison.OrdinalIgnoreCase));
        }

        if (options.DockerDiscoveryComposeOnly)
        {
            query = query.Where(container => !string.IsNullOrWhiteSpace(container.ComposeProject));
        }

        var prefixes = options.DockerDiscoveryExcludeNamePrefixes ?? [];
        query = query.Where(container => !prefixes.Any(prefix =>
            container.ContainerName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        return query
            .OrderBy(container => container.ComposeService ?? container.ContainerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ComposeServiceDefinition? FindOverlayMatch(
        DiscoveredContainer container,
        IReadOnlyList<ComposeServiceDefinition> overlayEntries)
    {
        foreach (var entry in overlayEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.ContainerName)
                && entry.ContainerName.Equals(container.ContainerName, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        foreach (var entry in overlayEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.ComposeService)
                && !string.IsNullOrWhiteSpace(container.ComposeService)
                && entry.ComposeService.Equals(container.ComposeService, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        foreach (var entry in overlayEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Id)
                && entry.Id.Equals(container.ComposeService ?? container.ContainerName, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static string ResolveServiceId(
        DiscoveredContainer container,
        ComposeServiceDefinition? overlayMatch,
        ISet<string> usedIds)
    {
        var candidates = new[]
        {
            overlayMatch?.Id,
            container.ComposeService,
            container.ContainerName
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = candidate.Trim();
            if (usedIds.Add(normalized))
            {
                return normalized;
            }
        }

        var fallback = container.ContainerName;
        var suffix = 2;
        while (!usedIds.Add(fallback))
        {
            fallback = $"{container.ContainerName}-{suffix}";
            suffix++;
        }

        return fallback;
    }

    private static string ResolveProjectName(
        IReadOnlyList<DiscoveredContainer> filtered,
        ComposeTopology overlay)
    {
        if (!string.IsNullOrWhiteSpace(overlay.ComposeProjectName))
        {
            return overlay.ComposeProjectName;
        }

        return filtered
            .Select(container => container.ComposeProject)
            .FirstOrDefault(project => !string.IsNullOrWhiteSpace(project))
            ?? "discovered";
    }

    private static string BuildAutoDescription(DiscoveredContainer container)
    {
        var parts = new List<string> { $"state={container.State}" };
        if (!string.IsNullOrWhiteSpace(container.ComposeProject))
        {
            parts.Add($"project={container.ComposeProject}");
        }

        if (!string.IsNullOrWhiteSpace(container.Ports))
        {
            parts.Add($"ports={container.Ports}");
        }

        return string.Join(", ", parts);
    }
}
