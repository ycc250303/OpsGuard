using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpsGuard.Core.Models;

namespace OpsGuard.Infrastructure.Network;

public sealed class HttpEndpointChecker : IHttpEndpointChecker
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpEndpointChecker> _logger;

    public HttpEndpointChecker(IHttpClientFactory httpClientFactory, ILogger<HttpEndpointChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DiagnosticResult<HttpProbeResult>> ProbeAsync(
        string serviceId,
        string healthUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(healthUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return DiagnosticResult<HttpProbeResult>.Fail($"Invalid HealthUrl: {healthUrl}");
        }

        var client = _httpClientFactory.CreateClient(HttpClientNames.Probe);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(uri, cancellationToken);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var healthy = statusCode is >= 200 and < 300;

            return DiagnosticResult<HttpProbeResult>.Ok(new HttpProbeResult
            {
                ServiceId = serviceId,
                Url = healthUrl,
                StatusCode = statusCode,
                LatencyMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                Healthy = healthy
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "HTTP probe failed for {ServiceId} {Url}", serviceId, healthUrl);
            return DiagnosticResult<HttpProbeResult>.Ok(new HttpProbeResult
            {
                ServiceId = serviceId,
                Url = healthUrl,
                LatencyMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                Healthy = false,
                Error = ex.Message
            });
        }
    }
}

public static class HttpClientNames
{
    public const string Probe = "OpsGuardProbe";
}
