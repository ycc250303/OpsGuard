using System.Globalization;
using System.Text.RegularExpressions;

namespace OpsGuard.Infrastructure.Docker;

public static partial class DockerLogSinceValidator
{
    [GeneratedRegex(@"^\d+[smh]$", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeDurationRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}:\d{2}(Z|[+-]\d{2}:\d{2})?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimestampRegex();

    public static bool TryNormalize(string? since, int maxLookbackHours, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(since))
        {
            return true;
        }

        since = since.Trim();
        if (RelativeDurationRegex().IsMatch(since))
        {
            if (!TryGetRelativeHours(since, out var hours))
            {
                error = $"Invalid relative since value: {since}";
                return false;
            }

            if (hours > maxLookbackHours)
            {
                error = $"since '{since}' exceeds max lookback {maxLookbackHours}h";
                return false;
            }

            normalized = since;
            return true;
        }

        if (IsoTimestampRegex().IsMatch(since))
        {
            normalized = since;
            return true;
        }

        error = $"Unsupported since format '{since}'. Use relative duration like 72h/30m or ISO date like 2024-01-01T00:00:00Z";
        return false;
    }

    private static bool TryGetRelativeHours(string since, out double hours)
    {
        hours = 0;
        var unit = since[^1];
        if (!double.TryParse(since[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            return false;
        }

        hours = unit switch
        {
            's' => value / 3600d,
            'm' => value / 60d,
            'h' => value,
            _ => -1
        };

        return hours >= 0;
    }
}
