using Atrium.Contracts;
using Atrium.Services.Storefront;

namespace Atrium.UnitTests;

/// <summary>
/// U3 — order pricing and its guards, extracted from the endpoint. Concept on show: a security-relevant
/// invariant proven in isolation — the server prices every line from the authoritative catalog, and the
/// request has no way to influence price (an <see cref="OrderItemRequest"/> carries only an id and a
/// quantity). Plus the defensive guards: empty order, unknown product, non-positive quantity.
/// </summary>
public class OrderPricingTests
{
    private static readonly IReadOnlyDictionary<int, ProductDto> Catalog = new Dictionary<
        int,
        ProductDto
    >
    {
        [1] = new(1, "Desk Mat", "Desk", 39m, "blurb"),
        [2] = new(2, "Headphones", "Audio", 199m, "blurb"),
    };

    [Fact]
    public void Prices_each_line_from_the_catalog_not_the_request()
    {
        var items = new[] { new OrderItemRequest(ProductId: 1, Quantity: 2) };

        var (lines, error) = OrderPricing.PriceOrder(items, Catalog);

        Assert.Null(error);
        var line = Assert.Single(lines!);
        Assert.Equal("Desk Mat", line.ProductName);
        Assert.Equal(39m, line.UnitPrice); // the catalog's price, the only source of truth
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void Empty_order_is_rejected()
    {
        var (lines, error) = OrderPricing.PriceOrder([], Catalog);

        Assert.Null(lines);
        Assert.Equal("The order has no items.", error);
    }

    [Fact]
    public void Unknown_product_is_rejected()
    {
        var items = new[] { new OrderItemRequest(ProductId: 999, Quantity: 1) };

        var (lines, error) = OrderPricing.PriceOrder(items, Catalog);

        Assert.Null(lines);
        Assert.Equal("Unknown product 999.", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Non_positive_quantity_is_rejected(int quantity)
    {
        var items = new[] { new OrderItemRequest(ProductId: 1, Quantity: quantity) };

        var (lines, error) = OrderPricing.PriceOrder(items, Catalog);

        Assert.Null(lines);
        Assert.Equal("Quantity for product 1 must be positive.", error);
    }
}
