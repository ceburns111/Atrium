using Atrium.Contracts;
using Atrium.Design;
using Microsoft.Extensions.Logging;

namespace Atrium.Modules.Storefront.Orders;

/// <summary>
/// Typed client for the Storefront app vertical's order API, reached through the gateway. Attaches the
/// signed-in user's access token (shared via <see cref="AccessTokenHolder"/>) so orders are placed and
/// listed as that user. Both calls ride the shared <see cref="TypedClientSendExtensions"/> pipeline,
/// which keeps session expiry ordered before the generic success check.
/// </summary>
public sealed class OrdersClient(
    HttpClient http,
    AccessTokenHolder tokens,
    ILogger<OrdersClient> logger
)
{
    // Never returns null: an unexpected empty success body throws (with the request in the message)
    // instead of handing the checkout a phantom "Order #0" to guard against.
    public Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default) =>
        http.SendForJsonAsync<OrderDto>(
            HttpMethod.Post,
            "storefront/orders",
            tokens,
            logger,
            request,
            ct
        );

    public Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct = default) =>
        http.SendForJsonAsync<IReadOnlyList<OrderDto>>(
            HttpMethod.Get,
            "storefront/orders",
            tokens,
            logger,
            ct: ct
        );
}
