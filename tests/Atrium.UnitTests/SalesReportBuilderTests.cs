using Atrium.Contracts;
using Atrium.Services.Storefront;

namespace Atrium.UnitTests;

/// <summary>
/// U2 — the sales report's aggregation, extracted from the endpoint into a pure function so it can be
/// tested with plain data. Concept on show: isolating cross-service composition logic (order rows +
/// a product→category map) from IO, then asserting the grouping, the "Other" fallback, the ordering,
/// and the rolled-up totals.
/// </summary>
public class SalesReportBuilderTests
{
    private static ProductSalesRow Sale(string product, int units, decimal revenue) =>
        new(product, units, revenue);

    [Fact]
    public void Buckets_products_into_their_categories_and_sums_each()
    {
        var sales = new[]
        {
            Sale("Desk Mat", 2, 80m),
            Sale("Monitor Shelf", 1, 90m),
            Sale("Headphones", 3, 600m),
        };
        var categoryByProduct = new Dictionary<string, string>
        {
            ["Desk Mat"] = "Desk",
            ["Monitor Shelf"] = "Desk",
            ["Headphones"] = "Audio",
        };

        var report = SalesReportBuilder.Build(sales, orderCount: 4, categoryByProduct);

        var desk = report.ByCategory.Single(c => c.Category == "Desk");
        Assert.Equal(170m, desk.Revenue); // 80 + 90
        Assert.Equal(3, desk.Units); // 2 + 1
    }

    [Fact]
    public void Products_the_catalog_does_not_know_fall_into_other()
    {
        var sales = new[] { Sale("Mystery Gadget", 1, 50m) };

        var report = SalesReportBuilder.Build(
            sales,
            orderCount: 1,
            new Dictionary<string, string>() // empty map — nothing resolves
        );

        Assert.Equal("Other", Assert.Single(report.ByCategory).Category);
    }

    [Fact]
    public void Categories_are_ordered_by_revenue_descending()
    {
        var sales = new[]
        {
            Sale("Desk Mat", 1, 40m), // Desk: 40
            Sale("Headphones", 1, 600m), // Audio: 600
        };
        var categoryByProduct = new Dictionary<string, string>
        {
            ["Desk Mat"] = "Desk",
            ["Headphones"] = "Audio",
        };

        var report = SalesReportBuilder.Build(sales, orderCount: 2, categoryByProduct);

        Assert.Equal(["Audio", "Desk"], report.ByCategory.Select(c => c.Category));
    }

    [Fact]
    public void CategoryByProductName_collapses_duplicate_names_instead_of_throwing()
    {
        // Names aren't unique (admin can create two "Walnut Monitor Shelf" products); a plain
        // ToDictionary(p => p.Name) throws on the duplicate key. Regression for that 500 on /reports.
        var products = new[]
        {
            new ProductDto(1, "Walnut Monitor Shelf", "Desk", 89m, "blurb"),
            new ProductDto(2, "Walnut Monitor Shelf", "Storage", 89m, "dup name"),
            new ProductDto(3, "Headphones", "Audio", 199m, "blurb"),
        };

        var map = SalesReportBuilder.CategoryByProductName(products);

        Assert.Equal("Desk", map["Walnut Monitor Shelf"]); // first wins
        Assert.Equal("Audio", map["Headphones"]);
    }

    [Fact]
    public void Totals_roll_up_the_categories_and_pass_the_order_count_through()
    {
        var sales = new[] { Sale("Desk Mat", 2, 80m), Sale("Headphones", 3, 600m) };
        var categoryByProduct = new Dictionary<string, string>
        {
            ["Desk Mat"] = "Desk",
            ["Headphones"] = "Audio",
        };

        var report = SalesReportBuilder.Build(sales, orderCount: 7, categoryByProduct);

        Assert.Equal(680m, report.TotalRevenue);
        Assert.Equal(5, report.TotalUnits);
        Assert.Equal(7, report.TotalOrders); // passed straight through, not derived from lines
    }
}
