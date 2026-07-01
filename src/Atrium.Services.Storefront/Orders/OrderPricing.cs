using Atrium.Contracts;

namespace Atrium.Services.Storefront.Orders;

/// <summary>
/// Turns the requested items into priced order lines, lifted out of <see cref="OrdersEndpoints"/> so
/// the rules can be unit-tested without HTTP or a database. The security-relevant point: the price on
/// every line comes from the <em>authoritative catalog</em>, never from the client — the request only
/// says which product and how many. Returns the priced lines, or an <c>Error</c> describing why not.
/// </summary>
public static class OrderPricing
{
    public static (IReadOnlyList<OrderLineDto>? Lines, string? Error) PriceOrder(
        IReadOnlyList<OrderItemRequest> items,
        IReadOnlyDictionary<int, ProductDto> catalog
    )
    {
        if (items.Count == 0)
        {
            return (null, "The order has no items.");
        }

        var lines = new List<OrderLineDto>();
        foreach (var item in items)
        {
            if (!catalog.TryGetValue(item.ProductId, out var product))
            {
                return (null, $"Unknown product {item.ProductId}.");
            }
            if (item.Quantity <= 0)
            {
                return (null, $"Quantity for product {item.ProductId} must be positive.");
            }
            // Price from the catalog, not from anything the caller supplied.
            lines.Add(new OrderLineDto(product.Name, product.Price, item.Quantity));
        }

        return (lines, null);
    }
}
