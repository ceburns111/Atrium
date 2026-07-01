using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Atrium.Contracts;
using Atrium.Design;
using Microsoft.Extensions.Logging;

namespace Atrium.Modules.Storefront.Orders;

/// <summary>
/// Typed client for the Storefront app vertical's order API, reached through the gateway. Attaches the
/// signed-in user's access token (shared via <see cref="AccessTokenHolder"/>) so orders are placed and
/// listed as that user.
/// </summary>
public sealed class OrdersClient(
    HttpClient http,
    AccessTokenHolder tokens,
    ILogger<OrdersClient> logger
)
{
    public async Task<OrderDto?> CreateAsync(
        CreateOrderRequest request,
        CancellationToken ct = default
    )
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "storefront/orders")
        {
            Content = JsonContent.Create(request),
        };
        Authorize(message);
        using var response = await http.SendAsync(message, ct);
        LogIfUnsuccessful(logger, message, response);
        response.ThrowIfSessionExpired();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(ct);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "storefront/orders");
        Authorize(message);
        using var response = await http.SendAsync(message, ct);
        LogIfUnsuccessful(logger, message, response);
        response.ThrowIfSessionExpired();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(ct) ?? [];
    }

    private void Authorize(HttpRequestMessage message)
    {
        if (!string.IsNullOrEmpty(tokens.AccessToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokens.AccessToken
            );
        }
    }

    // Structured Warning at the downstream seam: session expiry (401) vs. any other non-success. No auth
    // header or token is logged — only method, path and status.
    private static void LogIfUnsuccessful(
        ILogger logger,
        HttpRequestMessage request,
        HttpResponseMessage response
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
