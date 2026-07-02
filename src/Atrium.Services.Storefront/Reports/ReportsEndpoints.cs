using Atrium.Contracts;
using Atrium.Services.Storefront.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atrium.Services.Storefront.Reports;

/// <summary>
/// The storefront analytics surface: aggregate sales bucketed into product categories. Order data is
/// owned by this vertical; the category each product belongs to is read from the Catalog core service
/// (the same "slice calls core" bearer relay used to price orders), then joined here.
/// </summary>
public static class ReportsEndpoints
{
    // Mapped onto the parent "/storefront" group (auth applied there), so this owns only "/reports".
    // Analytics is admin-only: the parent supplies authentication, this subtree adds the admin role
    // gate — matching the admin-gated Reports page/nav so the API can't be reached by a plain customer.
    public static void MapReportEndpoints(this IEndpointRouteBuilder storefront)
    {
        var reports = storefront
            .MapGroup("/reports")
            .WithTags("Reports")
            .RequireAuthorization("admin");

        reports.MapGet("/sales", GetSalesReport);
    }

    private static async Task<Ok<SalesReportDto>> GetSalesReport(
        IReportRepository repository,
        IStorefrontCatalogClient catalog,
        CancellationToken ct
    )
    {
        var sales = await repository.GetSalesByProductAsync(ct);
        var orderCount = await repository.GetOrderCountAsync(ct);
        // Product names aren't unique, so dedupe when mapping name → category (see the helper's note).
        var categoryByProduct = SalesReportBuilder.CategoryByProductName(
            await catalog.GetProductsAsync(ct)
        );

        // The bucketing is a pure function (see SalesReportBuilder) so it can be unit-tested alone.
        return TypedResults.Ok(SalesReportBuilder.Build(sales, orderCount, categoryByProduct));
    }
}
