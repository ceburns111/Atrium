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
