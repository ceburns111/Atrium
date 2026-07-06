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
    /// <summary>Per-line quantity ceiling — generous for a human, far below anything that could
    /// overflow the DECIMAL(10,2) Total column at any plausible catalog price.</summary>
    public const int MaxQuantity = 1_000;

    /// <summary>Per-order total ceiling, kept well under DECIMAL(10,2)'s 99,999,999.99 maximum so an
    /// absurd order is a 400 at the boundary, not an arithmetic-overflow 500 in SQL.</summary>
    public const decimal MaxOrderTotal = 1_000_000m;

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
            if (item.Quantity > MaxQuantity)
            {
                return (
                    null,
                    $"Quantity for product {item.ProductId} exceeds the maximum of {MaxQuantity}."
                );
            }
            // Price from the catalog, not from anything the caller supplied.
            lines.Add(new OrderLineDto(product.Name, product.Price, item.Quantity));
        }

        if (lines.Sum(l => l.UnitPrice * l.Quantity) > MaxOrderTotal)
        {
            return (null, $"The order total exceeds the maximum of {MaxOrderTotal:N0}.");
        }

        return (lines, null);
    }
}
