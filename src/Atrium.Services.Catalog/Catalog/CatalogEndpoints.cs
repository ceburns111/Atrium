using Atrium.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Catalog.Catalog;

/// <summary>
/// The catalog HTTP surface, organized minimal-API style: one MapGroup, handlers as named static
/// methods (testable, no inline lambdas), TypedResults for compile-time-checked responses.
/// </summary>
public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        // Every catalog read requires an authenticated caller (a valid Keycloak JWT).
        var catalog = app.MapGroup("/catalog").WithTags("Catalog").RequireAuthorization();

        catalog.MapGet("/products", GetProducts);
        catalog.MapGet("/categories", GetCategories);

        // Writes are back-office only: on top of the group's auth, they require the admin realm role.
        catalog.MapPost("/products", CreateProduct).RequireAuthorization("admin");

        catalog.MapPut("/products/{id:int}", UpdateProduct).RequireAuthorization("admin");
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

    private static async Task<Results<Ok<ProductDto>, BadRequest<string>>> CreateProduct(
        CreateProductRequest request,
        ICatalogRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken ct
    )
    {
        var error = await Validate(request.Name, request.Category, request.Price, repository, ct);
        if (error is not null)
        {
            loggerFactory
                .CreateLogger(typeof(CatalogEndpoints))
                .LogWarning("Product create rejected: {Reason}", error);
            return TypedResults.BadRequest(error);
        }
        // Create always yields a row (the sproc guards the category), so the result is non-null.
        return TypedResults.Ok((await repository.CreateProductAsync(request, ct))!);
    }

    private static async Task<Results<Ok<ProductDto>, NotFound, BadRequest<string>>> UpdateProduct(
        int id,
        UpdateProductRequest request,
        ICatalogRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken ct
    )
    {
        var logger = loggerFactory.CreateLogger(typeof(CatalogEndpoints));
        var error = await Validate(request.Name, request.Category, request.Price, repository, ct);
        if (error is not null)
        {
            logger.LogWarning("Product {ProductId} update rejected: {Reason}", id, error);
            return TypedResults.BadRequest(error);
        }
        var product = await repository.UpdateProductAsync(id, request, ct);
        if (product is null)
        {
            logger.LogInformation("Product {ProductId} update matched no existing product", id);
            return TypedResults.NotFound();
        }
        return TypedResults.Ok(product);
    }

    // Shared validation for writes: a real name, a positive price, and a category that actually exists.
    private static async Task<string?> Validate(
        string name,
        string category,
        decimal price,
        ICatalogRepository repository,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }
        if (price <= 0)
        {
            return "Price must be positive.";
        }
        var categories = await repository.GetCategoriesAsync(ct);
        return categories.Any(c => c.Name == category) ? null : $"Unknown category '{category}'.";
    }
}
