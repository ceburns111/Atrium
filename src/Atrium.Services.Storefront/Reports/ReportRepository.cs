using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Atrium.Services.Storefront.Reports;

/// <summary>Dapper-backed analytics reads over the order tables (sproc-only, like the order store).</summary>
public sealed class ReportRepository(SqlConnection db) : IReportRepository
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
        return rows.AsList();
    }

    public Task<int> GetOrderCountAsync(CancellationToken ct = default) =>
        db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "dbo.usp_Report_OrderCount",
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            )
        );
}
