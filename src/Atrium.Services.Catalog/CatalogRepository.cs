using System.Data;
using Atrium.Contracts;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Atrium.Services.Catalog;

/// <summary>
/// Dapper-backed repository. Every read goes through a stored procedure (no inline SQL here); the
/// <see cref="SqlConnection"/> is the Aspire-injected "catalogdb". Product rows are mapped to DTOs by
/// the source-generated <see cref="CatalogMapper"/>.
/// </summary>
public sealed class CatalogRepository(SqlConnection db) : ICatalogRepository
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        string? category,
        CancellationToken ct = default
    )
    {
        var rows = await db.QueryAsync<ProductRow>(
            new CommandDefinition(
                "dbo.usp_Product_GetList",
                new { CategoryName = category },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            )
        );
        return CatalogMapper.ToDtos(rows.AsList());
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await db.QueryAsync<CategoryDto>(
            new CommandDefinition(
                "dbo.usp_Category_GetList",
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            )
        );
        return categories.AsList();
    }
}
