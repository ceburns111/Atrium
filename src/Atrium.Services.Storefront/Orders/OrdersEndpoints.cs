using Atrium.Contracts;
using Atrium.Services.Storefront.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Storefront.Orders;

/// <summary>
/// The Storefront vertical's HTTP surface (minimal-API style): create and list the signed-in user's
/// orders. Prices are taken from the Catalog core service, never trusted from the client.
/// </summary>
public static class OrdersEndpoints
{
    // Mapped onto the parent "/storefront" group (auth applied there), so this owns only "/orders".
    public static void MapOrderEndpoints(this IEndpointRouteBuilder storefront)
    {
        var orders = storefront.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("/", CreateOrder);
        orders.MapGet("/", GetOrders);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>>> CreateOrder(
        CreateOrderRequest request,
        HttpContext http,
        IOrderRepository repository,
        StorefrontCatalogClient catalog,
        ILoggerFactory loggerFactory,
        CancellationToken ct
    )
    {
        // The idempotency key is required: it's what lets a retry dedupe instead of duplicating. Reject
        // an empty key rather than let Guid.Empty collide on the unique index across unrelated orders.
        if (request.IdempotencyKey == Guid.Empty)
        {
            return TypedResults.BadRequest("An idempotency key is required.");
        }

        // Price every line from the authoritative catalog, not from the client (see OrderPricing).
        var products = (await catalog.GetProductsAsync(ct)).ToDictionary(p => p.Id);
        var (lines, error) = OrderPricing.PriceOrder(request.Items, products);
        if (error is not null)
        {
            loggerFactory
                .CreateLogger(typeof(OrdersEndpoints))
                .LogWarning("Order rejected during pricing: {Reason}", error);
            return TypedResults.BadRequest(error);
        }

        var userName = http.User.Identity?.Name ?? "unknown";
        // error was null, so lines is populated.
        var orderId = await repository.CreateAsync(userName, request.IdempotencyKey, lines!, ct);
        var total = lines!.Sum(l => l.UnitPrice * l.Quantity);
        return TypedResults.Ok(new OrderDto(orderId, DateTime.UtcNow, total, lines!));
    }

    private static async Task<Ok<IReadOnlyList<OrderDto>>> GetOrders(
        HttpContext http,
        IOrderRepository repository,
        CancellationToken ct
    )
    {
        var userName = http.User.Identity?.Name ?? "unknown";
        return TypedResults.Ok(await repository.GetOrdersAsync(userName, ct));
    }
}
