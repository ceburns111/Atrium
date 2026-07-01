using System.Data;
using Atrium.Contracts;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Catalog.Catalog;

/// <summary>The data seam the endpoints depend on — keeps handlers ignorant of Dapper and the sprocs.</summary>
public interface ICatalogRepository
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        string? category,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    Task<ProductDto?> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken ct = default
    );
    Task<ProductDto?> UpdateProductAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken ct = default
    );
}

/// <summary>
/// Dapper-backed repository. Every read goes through a stored procedure (no inline SQL here); the
/// <see cref="SqlConnection"/> is the Aspire-injected "catalogdb". Product rows are mapped to DTOs by
/// the source-generated <see cref="CatalogMapper"/>.
/// </summary>
public sealed class CatalogRepository(SqlConnection db, ILogger<CatalogRepository> logger)
    : ICatalogRepository
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

    public async Task<ProductDto?> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken ct = default
    )
    {
        var product = await WriteProductAsync(
            "dbo.usp_Product_Create",
            new
            {
                request.Name,
                CategoryName = request.Category,
                request.Price,
                request.Blurb,
            },
            ct
        );
        if (product is not null)
        {
            logger.LogInformation(
                "Product {ProductId} created in category {Category}",
                product.Id,
                product.Category
            );
        }
        return product;
    }

    public async Task<ProductDto?> UpdateProductAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken ct = default
    )
    {
        var product = await WriteProductAsync(
            "dbo.usp_Product_Update",
            new
            {
                Id = id,
                request.Name,
                CategoryName = request.Category,
                request.Price,
                request.Blurb,
            },
            ct
        );
        if (product is not null)
        {
            logger.LogInformation("Product {ProductId} updated", product.Id);
        }
        return product;
    }

    // Both write sprocs SELECT the affected row back (empty for an unknown id), so one helper maps both.
    private async Task<ProductDto?> WriteProductAsync(
        string procedure,
        object parameters,
        CancellationToken ct
    )
    {
        try
        {
            var row = await db.QuerySingleOrDefaultAsync<ProductRow>(
                new CommandDefinition(
                    procedure,
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct
                )
            );
            return row is null ? null : CatalogMapper.ToDto(row);
        }
        catch (SqlException ex)
        {
            // A sproc THROW (e.g. 50001 for an unknown category) or other DB fault — log and rethrow so
            // the caller's error handling is unchanged.
            logger.LogWarning(
                ex,
                "Catalog write {Procedure} failed with SQL error {SqlErrorNumber}",
                procedure,
                ex.Number
            );
            throw;
        }
    }
}
