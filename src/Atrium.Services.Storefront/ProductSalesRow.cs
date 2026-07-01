namespace Atrium.Services.Storefront;

/// <summary>Per-product sales totals from <c>dbo.usp_Report_SalesByProduct</c>; re-bucketed into
/// categories by the reports endpoint using the Catalog product-&gt;category map.</summary>
public sealed record ProductSalesRow(string ProductName, int Units, decimal Revenue);
