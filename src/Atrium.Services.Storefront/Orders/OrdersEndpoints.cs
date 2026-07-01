using Atrium.Contracts;
using Atrium.Services.Storefront.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atrium.Services.Storefront.Orders;

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
        // Price every line from the authoritative catalog, not from the client (see OrderPricing).
        var products = (await catalog.GetProductsAsync(ct)).ToDictionary(p => p.Id);
        var (lines, error) = OrderPricing.PriceOrder(request.Items, products);
        if (error is not null)
        {
            return TypedResults.BadRequest(error);
        }

        var userName = http.User.Identity?.Name ?? "unknown";
        // error was null, so lines is populated.
        var orderId = await repository.CreateAsync(userName, lines!, ct);
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
