namespace Atrium.Services.Catalog.Catalog;

/// <summary>The row shape returned by <c>dbo.usp_Category_GetList</c> — the category name with its
/// product count. Mapped to the public <c>CategoryDto</c> by <see cref="CatalogMapper"/>.</summary>
public sealed record CategoryRow(string Name, int ProductCount);
