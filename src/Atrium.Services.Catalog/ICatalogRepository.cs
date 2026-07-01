using Atrium.Contracts;

namespace Atrium.Services.Catalog;

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
