namespace Atrium.Services.Catalog;

/// <summary>The row shape returned by <c>dbo.usp_Product_GetList</c> — note <c>CategoryName</c>,
/// joined from Categories. Mapped to the public <c>ProductDto</c> by <see cref="CatalogMapper"/>.</summary>
public sealed record ProductRow(
    int Id,
    string Name,
    string CategoryName,
    decimal Price,
    string Blurb
);
