using Atrium.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atrium.Services.Catalog;

/// <summary>
/// The catalog HTTP surface, organized minimal-API style: one MapGroup, handlers as named static
/// methods (testable, no inline lambdas), TypedResults for compile-time-checked responses.
/// </summary>
public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var catalog = app.MapGroup("/catalog").WithTags("Catalog");

        catalog.MapGet("/products", GetProducts);
        catalog.MapGet("/categories", GetCategories);
    }

    private static async Task<Ok<IReadOnlyList<ProductDto>>> GetProducts(
        ICatalogRepository repository,
        string? category,
        CancellationToken ct
    ) => TypedResults.Ok(await repository.GetProductsAsync(category, ct));

    private static async Task<Ok<IReadOnlyList<CategoryDto>>> GetCategories(
        ICatalogRepository repository,
        CancellationToken ct
    ) => TypedResults.Ok(await repository.GetCategoriesAsync(ct));
}
