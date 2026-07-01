using System.Net;
using System.Net.Http.Headers;
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
        if (!string.IsNullOrEmpty(tokens.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokens.AccessToken
            );
        }
        using var response = await http.SendAsync(request, ct);
        LogIfUnsuccessful(logger, request, response);
        response.ThrowIfSessionExpired();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new InvalidOperationException();
    }

    // Structured Warning at the downstream seam: session expiry (401) vs. any other non-success. No auth
    // header or token is logged — only method, path and status.
    private static void LogIfUnsuccessful(
        ILogger logger,
        HttpRequestMessage request,
        HttpResponseMessage response
    )
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning(
                "Session expired: {Method} {RequestUri} returned 401",
                request.Method,
                request.RequestUri
            );
        }
        else
        {
            logger.LogWarning(
                "Downstream {Method} {RequestUri} returned {StatusCode}",
                request.Method,
                request.RequestUri,
                (int)response.StatusCode
            );
        }
    }
}
