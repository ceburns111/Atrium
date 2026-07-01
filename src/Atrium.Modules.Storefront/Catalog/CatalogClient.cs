using System.Net.Http.Json;
using Atrium.Contracts;
using Atrium.Design;
using Microsoft.Extensions.Logging;

namespace Atrium.Modules.Storefront.Catalog;

/// <summary>
/// Typed client for the Catalog core service, reached through the gateway. Its HttpClient base address
/// is the logical "https+http://gateway", resolved by service discovery; it attaches the signed-in
/// user's access token (captured by the shell into <see cref="AccessTokenHolder"/>, which shares this
/// component-resolved scope) so the request is authorized end to end.
/// </summary>
public sealed class CatalogClient(
    HttpClient http,
    AccessTokenHolder tokens,
    ILogger<CatalogClient> logger
)
{
    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        string? category = null,
        CancellationToken ct = default
    )
    {
        var url = category is null
            ? "catalog/products"
            : $"catalog/products?category={Uri.EscapeDataString(category)}";
        return GetAsync<IReadOnlyList<ProductDto>>(url, ct);
    }

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<CategoryDto>>("catalog/categories", ct);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Authorize(tokens);
        using var response = await http.SendAsync(request, ct);
        response.LogIfUnsuccessful(logger, request);
        response.ThrowIfSessionExpired();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new InvalidOperationException();
    }
}
