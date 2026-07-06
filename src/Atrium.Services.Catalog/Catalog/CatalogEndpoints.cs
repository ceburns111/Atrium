using Atrium.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Catalog.Catalog;

/// <summary>
/// The catalog HTTP surface, organized minimal-API style: one MapGroup, handlers as named static
/// methods (testable, no inline lambdas), TypedResults for compile-time-checked responses.
/// </summary>
public static class CatalogEndpoints
{
    // usp_Product_Create/_Update raise this when the category name resolves to no row. The validator
    // below normally catches it first, but the check-then-write gap is real, so the sproc's THROW is
    // mapped to the same 400 the validator produces instead of escaping as a 500.
    private const int UnknownCategorySqlError = 50001;

    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        // The group requires an authenticated caller by default; the storefront is browsable signed-out,
        // so each read opts back out with .AllowAnonymous() (that metadata wins over the group policy).
        var catalog = app.MapGroup("/catalog").WithTags("Catalog").RequireAuthorization();

        catalog.MapGet("/products", GetProducts).AllowAnonymous();
        catalog.MapGet("/categories", GetCategories).AllowAnonymous();

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
        var logger = loggerFactory.CreateLogger(typeof(CatalogEndpoints));
        var error = await Validate(
            request.Name,
            request.Category,
            request.Price,
            request.Blurb,
            repository,
            ct
        );
        if (error is not null)
        {
            logger.LogWarning("Product create rejected: {Reason}", error);
            return TypedResults.BadRequest(error);
        }
        try
        {
            // The sproc guards the category with THROW 50001 (mapped below), so a non-throwing
            // create always selects the inserted row back — the result is non-null.
            return TypedResults.Ok((await repository.CreateProductAsync(request, ct))!);
        }
        catch (SqlException ex) when (ex.Number == UnknownCategorySqlError)
        {
            logger.LogWarning(
                "Product create rejected: unknown category {Category}",
                request.Category
            );
            return TypedResults.BadRequest($"Unknown category '{request.Category}'.");
        }
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
        var error = await Validate(
            request.Name,
            request.Category,
            request.Price,
            request.Blurb,
            repository,
            ct
        );
        if (error is not null)
        {
            logger.LogWarning("Product {ProductId} update rejected: {Reason}", id, error);
            return TypedResults.BadRequest(error);
        }
        ProductDto? product;
        try
        {
            product = await repository.UpdateProductAsync(id, request, ct);
        }
        catch (SqlException ex) when (ex.Number == UnknownCategorySqlError)
        {
            logger.LogWarning(
                "Product {ProductId} update rejected: unknown category {Category}",
                id,
                request.Category
            );
            return TypedResults.BadRequest($"Unknown category '{request.Category}'.");
        }
        if (product is null)
        {
            logger.LogInformation("Product {ProductId} update matched no existing product", id);
            return TypedResults.NotFound();
        }
        return TypedResults.Ok(product);
    }

    // Shared validation for writes: a real name, a positive price, a blurb that fits the schema's
    // NVARCHAR(256) NOT NULL column, and a category that actually exists.
    private static async Task<string?> Validate(
        string name,
        string category,
        decimal price,
        string blurb,
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
        if (string.IsNullOrWhiteSpace(blurb))
        {
            return "Blurb is required.";
        }
        if (blurb.Length > 256)
        {
            return "Blurb must be 256 characters or fewer.";
        }
        var categories = await repository.GetCategoriesAsync(ct);
        // Case-insensitive on purpose: the database compares names under its default collation
        // (case-insensitive), so an ordinal check here would 400 input SQL happily accepts.
        return categories.Select(c => c.Name).Contains(category, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"Unknown category '{category}'.";
    }
}
