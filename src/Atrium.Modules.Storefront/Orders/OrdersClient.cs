using System.Net.Http.Headers;
using System.Net.Http.Json;
using Atrium.Contracts;
using Atrium.Design;

namespace Atrium.Modules.Storefront.Orders;

/// <summary>
/// Typed client for the Storefront app vertical's order API, reached through the gateway. Attaches the
/// signed-in user's access token (shared via <see cref="AccessTokenHolder"/>) so orders are placed and
/// listed as that user.
/// </summary>
public sealed class OrdersClient(HttpClient http, AccessTokenHolder tokens)
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
        response.ThrowIfSessionExpired();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(ct);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "storefront/orders");
        Authorize(message);
        using var response = await http.SendAsync(message, ct);
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
}
