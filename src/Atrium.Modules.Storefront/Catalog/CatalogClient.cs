using System.Net.Http.Json;
using Atrium.Contracts;

namespace Atrium.Modules.Storefront.Catalog;

/// <summary>
/// Typed client for the Catalog core service, reached through the gateway. Its HttpClient base address
/// is the logical "https+http://gateway", resolved at runtime by service discovery.
/// </summary>
public sealed class CatalogClient(HttpClient http)
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        string? category = null,
        CancellationToken ct = default
    )
    {
        var url = category is null
            ? "catalog/products"
            : $"catalog/products?category={Uri.EscapeDataString(category)}";
        return await http.GetFromJsonAsync<IReadOnlyList<ProductDto>>(url, ct) ?? [];
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(
        CancellationToken ct = default
    ) => await http.GetFromJsonAsync<IReadOnlyList<CategoryDto>>("catalog/categories", ct) ?? [];
}
