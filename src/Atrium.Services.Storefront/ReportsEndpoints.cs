using Atrium.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atrium.Services.Storefront;

/// <summary>
/// The storefront analytics surface: aggregate sales bucketed into product categories. Order data is
/// owned by this vertical; the category each product belongs to is read from the Catalog core service
/// (the same "slice calls core" bearer relay used to price orders), then joined here.
/// </summary>
public static class ReportsEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/storefront/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        reports.MapGet("/sales", GetSalesReport);
    }

    private static async Task<Ok<SalesReportDto>> GetSalesReport(
        IReportRepository repository,
        StorefrontCatalogClient catalog,
        CancellationToken ct
    )
    {
        var sales = await repository.GetSalesByProductAsync(ct);
        var orderCount = await repository.GetOrderCountAsync(ct);
        var categoryByProduct = (await catalog.GetProductsAsync(ct)).ToDictionary(
            p => p.Name,
            p => p.Category
        );

        // Order rows only carry the product name, so map each back to its category (unknowns -> "Other").
        var byCategory = sales
            .GroupBy(s => categoryByProduct.GetValueOrDefault(s.ProductName, "Other"))
            .Select(g => new CategorySalesDto(g.Key, g.Sum(s => s.Revenue), g.Sum(s => s.Units)))
            .OrderByDescending(c => c.Revenue)
            .ToList();

        return TypedResults.Ok(
            new SalesReportDto(
                byCategory.Sum(c => c.Revenue),
                orderCount,
                byCategory.Sum(c => c.Units),
                byCategory
            )
        );
    }
}
