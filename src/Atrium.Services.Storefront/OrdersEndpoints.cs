using Atrium.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atrium.Services.Storefront;

/// <summary>
/// The Storefront vertical's HTTP surface (minimal-API style): create and list the signed-in user's
/// orders. Prices are taken from the Catalog core service, never trusted from the client.
/// </summary>
public static class OrdersEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/storefront/orders").WithTags("Orders").RequireAuthorization();

        orders.MapPost("/", CreateOrder);
        orders.MapGet("/", GetOrders);
    }

    private static async Task<Results<Ok<OrderDto>, BadRequest<string>>> CreateOrder(
        CreateOrderRequest request,
        HttpContext http,
        IOrderRepository repository,
        StorefrontCatalogClient catalog,
        CancellationToken ct
    )
    {
        if (request.Items.Count == 0)
        {
            return TypedResults.BadRequest("The order has no items.");
        }

        // Price every line from the authoritative catalog, not from the client.
        var products = (await catalog.GetProductsAsync(ct)).ToDictionary(p => p.Id);
        var lines = new List<OrderLineDto>();
        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return TypedResults.BadRequest($"Unknown product {item.ProductId}.");
            }
            if (item.Quantity <= 0)
            {
                return TypedResults.BadRequest(
                    $"Quantity for product {item.ProductId} must be positive."
                );
            }
            lines.Add(new OrderLineDto(product.Name, product.Price, item.Quantity));
        }

        var userName = http.User.Identity?.Name ?? "unknown";
        var orderId = await repository.CreateAsync(userName, lines, ct);
        var total = lines.Sum(l => l.UnitPrice * l.Quantity);
        return TypedResults.Ok(new OrderDto(orderId, DateTime.UtcNow, total, lines));
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
