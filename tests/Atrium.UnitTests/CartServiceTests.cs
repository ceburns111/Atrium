using Atrium.Contracts;
using Atrium.Modules.Storefront.Cart;

namespace Atrium.UnitTests;

/// <summary>
/// U1 — the cart is a small in-memory state machine (the circuit-scoped basket). These tests exercise
/// its transitions directly: no mocks, no host, no DI. Concept on show: unit-testing stateful service
/// logic by driving it and asserting the resulting state.
/// </summary>
public class CartServiceTests
{
    private static ProductDto Product(int id, decimal price) =>
        new(id, $"Product {id}", "Desk", price, "blurb");

    [Fact]
    public void Add_new_product_creates_a_line_of_quantity_one()
    {
        var cart = new CartService();

        cart.Add(Product(1, 10m));

        var line = Assert.Single(cart.Lines);
        Assert.Equal(1, line.Product.Id);
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public void Add_same_product_twice_increments_rather_than_duplicating()
    {
        var cart = new CartService();
        var product = Product(1, 10m);

        cart.Add(product);
        cart.Add(product);

        var line = Assert.Single(cart.Lines);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void Total_and_count_sum_across_lines()
    {
        var cart = new CartService();
        cart.Add(Product(1, 10m)); // 1 × 10
        cart.Add(Product(2, 4.50m));
        cart.Add(Product(2, 4.50m)); // 2 × 4.50

        Assert.Equal(3, cart.Count); // 1 + 2 quantities
        Assert.Equal(19m, cart.Total); // 10 + 9
    }

    [Fact]
    public void Setting_quantity_to_zero_removes_the_line()
    {
        var cart = new CartService();
        cart.Add(Product(1, 10m));

        cart.SetQuantity(1, 0);

        Assert.Empty(cart.Lines);
    }

    [Fact]
    public void Setting_a_positive_quantity_replaces_the_count()
    {
        var cart = new CartService();
        cart.Add(Product(1, 10m));

        cart.SetQuantity(1, 5);

        Assert.Equal(5, Assert.Single(cart.Lines).Quantity);
    }

    [Fact]
    public void Changed_fires_on_mutation_so_the_ui_can_rerender()
    {
        var cart = new CartService();
        var fired = 0;
        cart.Changed += () => fired++;

        cart.Add(Product(1, 10m));

        Assert.Equal(1, fired);
    }

    // MergeRestored is the hydration seam: persisted lines land in a cart the user may already have
    // started filling (items added while the hydrate round-trip was in flight must survive).

    [Fact]
    public void MergeRestored_keeps_lines_added_before_hydration_completed()
    {
        var cart = new CartService();
        cart.Add(Product(2, 5m)); // added during the hydrate window

        cart.MergeRestored([new CartLine { Product = Product(1, 10m), Quantity = 2 }]);

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(1, cart.Lines.Single(l => l.Product.Id == 2).Quantity);
        Assert.Equal(2, cart.Lines.Single(l => l.Product.Id == 1).Quantity);
    }

    [Fact]
    public void MergeRestored_sums_quantities_when_the_same_product_is_on_both_sides()
    {
        var cart = new CartService();
        cart.Add(Product(1, 10m)); // fresh click during the hydrate window

        cart.MergeRestored([new CartLine { Product = Product(1, 10m), Quantity = 2 }]);

        var line = Assert.Single(cart.Lines);
        Assert.Equal(3, line.Quantity); // 2 restored + 1 added since
    }

    [Fact]
    public void MergeRestored_into_an_empty_cart_restores_the_persisted_lines()
    {
        var cart = new CartService();

        cart.MergeRestored([
            new CartLine { Product = Product(1, 10m), Quantity = 2 },
            new CartLine { Product = Product(2, 4.50m), Quantity = 1 },
        ]);

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(24.50m, cart.Total);
    }

    [Fact]
    public void MergeRestored_notifies_subscribers_once()
    {
        var cart = new CartService();
        var fired = 0;
        cart.Changed += () => fired++;

        cart.MergeRestored([
            new CartLine { Product = Product(1, 10m), Quantity = 1 },
            new CartLine { Product = Product(2, 4.50m), Quantity = 1 },
        ]);

        Assert.Equal(1, fired);
    }
}
