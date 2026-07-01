using System.Net;
using System.Net.Http.Json;
using Atrium.Contracts;
using Atrium.Design;
using Microsoft.Extensions.Logging;

namespace Atrium.Modules.Admin;

/// <summary>
/// Typed client for the Catalog core (through the gateway), including the admin-only writes. It attaches
/// the signed-in user's bearer token (from <see cref="AccessTokenHolder"/>); the write endpoints enforce
/// the admin role themselves, so writes translate a 403 into a friendly message rather than throwing.
/// </summary>
public sealed class AdminCatalogClient(
    HttpClient http,
    AccessTokenHolder tokens,
    ILogger<AdminCatalogClient> logger
)
{
    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<ProductDto>>("catalog/products", ct);

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<CategoryDto>>("catalog/categories", ct);

    public Task<(ProductDto? Product, string? Error)> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken ct = default
    ) => WriteAsync(HttpMethod.Post, "catalog/products", request, ct);

    public Task<(ProductDto? Product, string? Error)> UpdateProductAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken ct = default
    ) => WriteAsync(HttpMethod.Put, $"catalog/products/{id}", request, ct);

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

    private async Task<(ProductDto?, string?)> WriteAsync(
        HttpMethod method,
        string url,
        object body,
        CancellationToken ct
    )
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Authorize(tokens);
        using var response = await http.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<ProductDto>(ct), null);
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.LogIfUnsuccessful(logger, request);
        }
        // An expired token (401) is a dead session, not a per-request problem the page can toast past —
        // let the shell's boundary prompt a re-login. A 403 (wrong role) keeps you signed in, so it
        // stays an inline message.
        response.ThrowIfSessionExpired();
        // 403/400/404 are expected, translated to inline messages below — not logged as faults. Anything
        // else is an unexpected downstream failure worth a Warning before it throws.
        if (
            response.StatusCode
            is not (
                HttpStatusCode.Forbidden
                or HttpStatusCode.BadRequest
                or HttpStatusCode.NotFound
            )
        )
        {
            response.LogIfUnsuccessful(logger, request);
        }
        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => (null, "You need the admin role to change the catalog."),
            HttpStatusCode.BadRequest => (null, await response.Content.ReadAsStringAsync(ct)),
            HttpStatusCode.NotFound => (null, "That product no longer exists."),
            _ => throw new HttpRequestException(
                $"Catalog write failed ({(int)response.StatusCode})."
            ),
        };
    }
}
