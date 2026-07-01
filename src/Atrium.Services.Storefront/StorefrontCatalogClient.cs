using System.Net.Http.Headers;
using System.Net.Http.Json;
using Atrium.Contracts;

namespace Atrium.Services.Storefront;

/// <summary>
/// Internal client the Storefront vertical uses to call the Catalog core service (service-to-service).
/// It relays the caller's bearer token so the authenticated user's identity flows through to Catalog —
/// this is the "slice calls core" edge of the architecture, and why Storefront doesn't own product data.
/// </summary>
public sealed class StorefrontCatalogClient(HttpClient http, IHttpContextAccessor httpContext)
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "catalog/products");

        var incoming = httpContext.HttpContext?.Request.Headers.Authorization.ToString();
        if (
            !string.IsNullOrEmpty(incoming)
            && incoming.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        )
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(incoming);
        }

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProductDto>>(ct) ?? [];
    }
}
