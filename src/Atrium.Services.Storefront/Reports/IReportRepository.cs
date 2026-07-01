namespace Atrium.Services.Storefront.Reports;

/// <summary>Read model for storefront analytics — aggregates this vertical's own order tables.</summary>
public interface IReportRepository
{
    Task<IReadOnlyList<ProductSalesRow>> GetSalesByProductAsync(CancellationToken ct = default);
    Task<int> GetOrderCountAsync(CancellationToken ct = default);
}
