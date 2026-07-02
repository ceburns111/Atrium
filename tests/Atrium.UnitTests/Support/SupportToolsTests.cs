using Atrium.Contracts;
using Atrium.Services.Storefront.Support;

namespace Atrium.UnitTests.Support;

/// <summary>
/// The two support-agent tools answer only from the signed-in user's real data: an honest,
/// data-derived order status (no invented Shipped/Delivered) and a name-filtered catalog search.
/// </summary>
public class SupportToolsTests
{
    private static readonly OrderDto SampleOrder = new(
        Id: 42,
        PlacedAtUtc: new DateTime(2026, 6, 15, 9, 30, 0, DateTimeKind.Utc),
        Total: 59.98m,
        Lines: [new OrderLineDto("Widget", 19.99m, 2), new OrderLineDto("Gadget", 19.99m, 1)]
    );

    [Fact]
    public async Task GetOrderStatus_returns_honest_status_for_a_found_order()
    {
        var tools = new SupportTools(
            SupportTestDoubles.HttpContextFor("alice"),
            new FakeOrderRepository(SampleOrder),
            new FakeCatalogClient()
        );

        var result = await tools.GetOrderStatus(42);

        // 3 items total (2 + 1), honestly "Confirmed" — the store has no shipped/delivered state.
        var expected =
            $"Order #42 — Confirmed. Placed {SampleOrder.PlacedAtUtc:d}, 3 item(s), total {59.98m:C}.";
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetOrderStatus_scopes_the_lookup_to_the_signed_in_user()
    {
        var repo = new FakeOrderRepository(SampleOrder);
        var tools = new SupportTools(
            SupportTestDoubles.HttpContextFor("alice"),
            repo,
            new FakeCatalogClient()
        );

        await tools.GetOrderStatus(42);

        Assert.Equal("alice", repo.LastUserName);
    }

    [Fact]
    public async Task GetOrderStatus_returns_a_friendly_message_when_not_found()
    {
        var tools = new SupportTools(
            SupportTestDoubles.HttpContextFor("alice"),
            new FakeOrderRepository(order: null),
            new FakeCatalogClient()
        );

        var result = await tools.GetOrderStatus(999);

        Assert.Contains("couldn't find an order #999", result);
    }

    [Fact]
    public async Task FindProduct_returns_name_filtered_matches()
    {
        var tools = new SupportTools(
            SupportTestDoubles.HttpContextFor("alice"),
            new FakeOrderRepository(),
            new FakeCatalogClient(
                new ProductDto(1, "Red Widget", "Tools", 9.99m, "A widget"),
                new ProductDto(2, "Blue Gadget", "Tools", 14.99m, "A gadget")
            )
        );

        var result = await tools.FindProduct("widget");

        Assert.Contains("Red Widget", result);
        Assert.DoesNotContain("Blue Gadget", result);
    }

    [Fact]
    public async Task FindProduct_returns_nothing_found_when_no_match()
    {
        var tools = new SupportTools(
            SupportTestDoubles.HttpContextFor("alice"),
            new FakeOrderRepository(),
            new FakeCatalogClient(new ProductDto(1, "Red Widget", "Tools", 9.99m, "A widget"))
        );

        var result = await tools.FindProduct("sprocket");

        Assert.Contains("couldn't find any products", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindProduct_asks_for_a_keyword_when_query_is_blank(string query)
    {
        // A blank query must not match the whole catalog and return an arbitrary first five.
        var tools = new SupportTools(
            SupportTestDoubles.HttpContextFor("alice"),
            new FakeOrderRepository(),
            new FakeCatalogClient(new ProductDto(1, "Red Widget", "Tools", 9.99m, "A widget"))
        );

        var result = await tools.FindProduct(query);

        Assert.Contains("What product are you looking for", result);
        Assert.DoesNotContain("Red Widget", result);
    }
}
