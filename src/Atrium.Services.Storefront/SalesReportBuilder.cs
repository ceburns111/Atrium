using Atrium.Contracts;

namespace Atrium.Services.Storefront;

/// <summary>
/// Pure aggregation for the sales report, lifted out of <see cref="ReportsEndpoints"/> so it can be
/// unit-tested with no database and no Catalog call. Order rows only carry a product <em>name</em>, so
/// each is mapped to its category (products the catalog doesn't know fall to <c>"Other"</c>); revenue
/// and units are summed per category, ordered by revenue, and rolled up into the headline totals.
/// </summary>
public static class SalesReportBuilder
{
    /// <summary>
    /// Builds the product-name → category lookup the report buckets by. Order lines reference products
    /// by <em>name</em>, and names are not unique (admin can create two products with the same name), so
    /// this collapses duplicates to one category per name (first wins) rather than throwing on a
    /// duplicate key — the crash a plain <c>ToDictionary(p =&gt; p.Name)</c> would produce.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CategoryByProductName(
        IEnumerable<ProductDto> products
    ) => products.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First().Category);

    public static SalesReportDto Build(
        IReadOnlyList<ProductSalesRow> sales,
        int orderCount,
        IReadOnlyDictionary<string, string> categoryByProduct
    )
    {
        var byCategory = sales
            .GroupBy(s => categoryByProduct.GetValueOrDefault(s.ProductName, "Other"))
            .Select(g => new CategorySalesDto(g.Key, g.Sum(s => s.Revenue), g.Sum(s => s.Units)))
            .OrderByDescending(c => c.Revenue)
            .ToList();

        return new SalesReportDto(
            byCategory.Sum(c => c.Revenue),
            orderCount,
            byCategory.Sum(c => c.Units),
            byCategory
        );
    }
}
