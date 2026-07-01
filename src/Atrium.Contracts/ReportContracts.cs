namespace Atrium.Contracts;

/// <summary>Aggregated sales for one product category.</summary>
public sealed record CategorySalesDto(string Category, decimal Revenue, int Units);

/// <summary>A storefront sales report: headline totals plus a breakdown by product category.</summary>
public sealed record SalesReportDto(
    decimal TotalRevenue,
    int TotalOrders,
    int TotalUnits,
    IReadOnlyList<CategorySalesDto> ByCategory
);
