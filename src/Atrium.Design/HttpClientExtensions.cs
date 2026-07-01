using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Atrium.Design;

/// <summary>
/// Attaches the signed-in user's bearer token to an outgoing request. Shared by the module typed clients
/// so token attachment is written once rather than hand-rolled per client (see <see cref="AccessTokenHolder"/>).
/// </summary>
public static class HttpRequestAuthorizationExtensions
{
    public static void Authorize(this HttpRequestMessage request, AccessTokenHolder tokens)
    {
        if (!string.IsNullOrEmpty(tokens.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokens.AccessToken
            );
        }
    }
}

/// <summary>
/// Structured Warning at the downstream seam, shared by the module typed clients: session expiry (401)
/// vs. any other non-success. No auth header or token is logged — only method, path and status.
/// </summary>
public static class HttpResponseLoggingExtensions
{
    public static void LogIfUnsuccessful(
        this HttpResponseMessage response,
        ILogger logger,
        HttpRequestMessage request
    )
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning(
                "Session expired: {Method} {RequestUri} returned 401",
                request.Method,
                request.RequestUri
            );
        }
        else
        {
            logger.LogWarning(
                "Downstream {Method} {RequestUri} returned {StatusCode}",
                request.Method,
                request.RequestUri,
                (int)response.StatusCode
            );
        }
    }
}
