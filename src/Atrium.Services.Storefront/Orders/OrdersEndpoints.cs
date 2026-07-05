using Atrium.Contracts;
using Atrium.Services.Storefront.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Storefront.Orders;

/// <summary>
/// The Storefront vertical's HTTP surface (minimal-API style): create and list the signed-in user's
/// orders. Prices are taken from the Catalog core service, never trusted from the client.
/// </summary>
public static class OrdersEndpoints
{
    // usp_Order_Create raises this when the idempotency key was already committed by a different
    // user — surfaced as 409 rather than replaying (and leaking) someone else's order.
    private const int IdempotencyKeyConflictError = 50002;

    // Mapped onto the parent "/storefront" group (auth applied there), so this owns only "/orders".
    public static void MapOrderEndpoints(this IEndpointRouteBuilder storefront)
    {
        var orders = storefront.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("/", CreateOrder);
        orders.MapGet("/", GetOrders);
    }

    private static async Task<
        Results<Ok<OrderDto>, BadRequest<string>, Conflict<string>>
    > CreateOrder(
        CreateOrderRequest request,
        HttpContext http,
        IOrderRepository repository,
        IStorefrontCatalogClient catalog,
        ILoggerFactory loggerFactory,
        CancellationToken ct
    )
    {
        var logger = loggerFactory.CreateLogger(typeof(OrdersEndpoints));

        // Orders are stored per user name; a token without one would pool orders under a shared
        // sentinel bucket, so reject it outright.
        var userName = http.User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
        {
            return TypedResults.BadRequest("The authenticated caller has no user name.");
        }

        // The idempotency key is required: it's what lets a retry dedupe instead of duplicating. Reject
        // an empty key rather than let Guid.Empty collide on the unique index across unrelated orders.
        if (request.IdempotencyKey == Guid.Empty)
        {
            return TypedResults.BadRequest("An idempotency key is required.");
        }

        // Guard the shape before pricing touches it — a null Items would otherwise NRE into a 500.
        if (request.Items is not { Count: > 0 })
        {
            return TypedResults.BadRequest("The order has no items.");
        }

        // Price every line from the authoritative catalog, not from the client (see OrderPricing).
        var products = (await catalog.GetProductsAsync(ct)).ToDictionary(p => p.Id);
        var (lines, error) = OrderPricing.PriceOrder(request.Items, products);
        if (error is not null)
        {
            logger.LogWarning("Order rejected during pricing: {Reason}", error);
            return TypedResults.BadRequest(error);
        }

        int orderId;
        try
        {
            // error was null, so lines is populated.
            orderId = await repository.CreateAsync(userName, request.IdempotencyKey, lines!, ct);
        }
        catch (SqlException ex) when (ex.Number == IdempotencyKeyConflictError)
        {
            logger.LogWarning(
                "Order rejected: idempotency key {IdempotencyKey} is already in use by another user",
                request.IdempotencyKey
            );
            return TypedResults.Conflict("The idempotency key is already in use.");
        }

        // Return what was actually committed, not a re-priced reconstruction — on an idempotent
        // replay the stored order (its total, lines, and timestamp) is the truth, not this request.
        var order = await repository.GetByIdAsync(orderId, userName, ct);
        return order is not null
            ? TypedResults.Ok(order)
            : throw new InvalidOperationException($"Order {orderId} vanished after creation.");
    }

    private static async Task<Results<Ok<IReadOnlyList<OrderDto>>, BadRequest<string>>> GetOrders(
        HttpContext http,
        IOrderRepository repository,
        CancellationToken ct
    )
    {
        // Same rule as create: no user name, no order bucket to read from.
        var userName = http.User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
        {
            return TypedResults.BadRequest("The authenticated caller has no user name.");
        }
        return TypedResults.Ok(await repository.GetOrdersAsync(userName, ct));
    }
}
