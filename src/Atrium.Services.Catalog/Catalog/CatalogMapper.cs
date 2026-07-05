using Atrium.Contracts;
using Riok.Mapperly.Abstractions;

namespace Atrium.Services.Catalog.Catalog;

/// <summary>
/// Maps the internal row types (<see cref="ProductRow"/>, <see cref="CategoryRow"/>) to their public
/// DTOs. Mapperly generates the mapping code at compile time — no runtime reflection. The
/// <see cref="MapPropertyAttribute"/> renames <c>CategoryName</c> to <c>Category</c>, proving a real
/// (non-identity) mapping is being generated.
/// </summary>
[Mapper]
public static partial class CatalogMapper
{
    [MapProperty(nameof(ProductRow.CategoryName), nameof(ProductDto.Category))]
    public static partial ProductDto ToDto(ProductRow row);

    public static partial IReadOnlyList<ProductDto> ToDtos(IEnumerable<ProductRow> rows);

    public static partial CategoryDto ToDto(CategoryRow row);

    public static partial IReadOnlyList<CategoryDto> ToDtos(IEnumerable<CategoryRow> rows);
}
