using System.Text.Json;
using OpsGuard.Core.Models;

namespace OpsGuard.Core.Topology;

/// <summary>
/// 可选 overlay JSON：补充 HealthUrl、DisplayName、serviceId 别名等；文件缺失时不报错。
/// </summary>
public sealed class TopologyOverlayProvider : ITopologyOverlayProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public TopologyOverlayProvider(string? overlayFilePath)
    {
        OverlayFilePath = overlayFilePath;

        if (string.IsNullOrWhiteSpace(overlayFilePath) || !File.Exists(overlayFilePath))
        {
            Overlay = CreateEmptyOverlay();
            IsLoaded = false;
            return;
        }

        try
        {
            var json = File.ReadAllText(overlayFilePath);
            var overlay = JsonSerializer.Deserialize<ComposeTopology>(json, JsonOptions)
                ?? throw new TopologyLoadException("Overlay JSON deserialized to null.");

            ValidateOverlay(overlay);
            Overlay = overlay;
            IsLoaded = true;
        }
        catch (TopologyLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TopologyLoadException($"Failed to load topology overlay from {overlayFilePath}.", ex);
        }
    }

    public string? OverlayFilePath { get; }

    public bool IsLoaded { get; }

    public ComposeTopology Overlay { get; }

    internal static void ValidateOverlay(ComposeTopology overlay)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in overlay.Services)
        {
            if (string.IsNullOrWhiteSpace(service.Id)
                && string.IsNullOrWhiteSpace(service.ContainerName)
                && string.IsNullOrWhiteSpace(service.ComposeService))
            {
                throw new TopologyLoadException(
                    "Overlay service entry must specify at least one of Id, ContainerName, or ComposeService.");
            }

            if (!string.IsNullOrWhiteSpace(service.Id) && !ids.Add(service.Id))
            {
                throw new TopologyLoadException($"Duplicate overlay service Id: {service.Id}");
            }
        }
    }

    private static ComposeTopology CreateEmptyOverlay() => new()
    {
        Host = new HostInfo { DisplayName = "本机 Docker 宿主机" },
        Services = []
    };
}
