using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Storefront.Reports;

/// <summary>Read model for storefront analytics — aggregates this vertical's own order tables.</summary>
public interface IReportRepository
{
    Task<IReadOnlyList<ProductSalesRow>> GetSalesByProductAsync(CancellationToken ct = default);
    Task<int> GetOrderCountAsync(CancellationToken ct = default);
}

/// <summary>Dapper-backed analytics reads over the order tables (sproc-only, like the order store).</summary>
public sealed class ReportRepository(SqlConnection db, ILogger<ReportRepository> logger)
    : IReportRepository
{
    public async Task<IReadOnlyList<ProductSalesRow>> GetSalesByProductAsync(
        CancellationToken ct = default
    )
    {
        var rows = await db.QueryAsync<ProductSalesRow>(
            new CommandDefinition(
                "dbo.usp_Report_SalesByProduct",
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            )
        );
        var sales = rows.AsList();
        logger.LogInformation(
            "Sales-by-product report returned {RowCount} product row(s)",
            sales.Count
        );
        return sales;
    }

    public async Task<int> GetOrderCountAsync(CancellationToken ct = default)
    {
        var count = await db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "dbo.usp_Report_OrderCount",
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            )
        );
        logger.LogInformation("Order-count report returned {OrderCount}", count);
        return count;
    }
}
