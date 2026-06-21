using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace OpsGuard.Infrastructure.Network;

/// <summary>
/// DashScope 等 LLM HTTPS 客户端。macOS 上默认吊销检查常因 OCSP 不可达报 RevocationStatusUnknown。
/// </summary>
public static class OpsGuardLlmHttpClient
{
    private static readonly HttpClient Shared = Create();

    public static HttpClient Instance => Shared;

    private static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (_, _, chain, errors) =>
                    ValidateServerCertificate(chain, errors)
            }
        };

        return new HttpClient(handler);
    }

    internal static bool ValidateServerCertificate(X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        if (errors == SslPolicyErrors.RemoteCertificateChainErrors && chain is not null)
        {
            return chain.ChainStatus.All(static status =>
                status.Status is X509ChainStatusFlags.RevocationStatusUnknown
                    or X509ChainStatusFlags.OfflineRevocation);
        }

        return false;
    }
}
