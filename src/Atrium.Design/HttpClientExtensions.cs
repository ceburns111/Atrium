using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Atrium.Design;

/// <summary>
/// The one mandated request pipeline for module typed clients (see ADR-0004 / ADR-0008):
/// Authorize → SendAsync → LogIfUnsuccessful → ThrowIfSessionExpired → EnsureSuccessStatusCode →
/// ReadFromJsonAsync. Clients that need bespoke error translation (e.g. the Admin write path's
/// status-to-message tuples) keep their own send loop but must preserve the same ordering.
/// </summary>
public static class TypedClientSendExtensions
{
    /// <summary>
    /// Sends an authorized JSON request through the gateway and deserializes the response body.
    /// Throws <see cref="SessionExpiredException"/> on 401, <see cref="HttpRequestException"/> on
    /// any other non-success, and <see cref="InvalidOperationException"/> on an empty success body.
    /// </summary>
    public static async Task<T> SendForJsonAsync<T>(
        this HttpClient http,
        HttpMethod method,
        string url,
        AccessTokenHolder tokens,
        ILogger logger,
        object? body = null,
        CancellationToken ct = default
    )
        where T : class
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        request.Authorize(tokens);
        using var response = await http.SendAsync(request, ct);
        response.LogIfUnsuccessful(logger, request);
        // INVARIANT: ThrowIfSessionExpired() MUST run BEFORE EnsureSuccessStatusCode(), so an
        // expired token (401) surfaces as the typed, recoverable SessionExpiredException the
        // shell's SessionErrorBoundary turns into a re-login prompt — not a generic
        // HttpRequestException that tears the circuit down (ADR-0008).
        response.ThrowIfSessionExpired();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new InvalidOperationException(
                $"{method} {url} succeeded but returned an empty body where {typeof(T).Name} was expected."
            );
    }
}

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
