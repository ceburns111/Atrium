using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Atrium.Contracts;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Storefront.Catalog;

/// <summary>
/// Read surface of the Catalog core the Storefront vertical depends on. Extracted (mirroring
/// <c>IOrderRepository</c>) so callers — endpoints and the support-agent product tool — can be
/// unit-tested over a fake instead of a live <see cref="HttpClient"/>.
/// </summary>
public interface IStorefrontCatalogClient
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken ct = default);
}

/// <summary>
/// Internal client the Storefront vertical uses to call the Catalog core service (service-to-service).
/// It relays the caller's bearer token so the authenticated user's identity flows through to Catalog —
/// this is the "slice calls core" edge of the architecture, and why Storefront doesn't own product data.
/// </summary>
public sealed class StorefrontCatalogClient(
    HttpClient http,
    IHttpContextAccessor httpContext,
    ILogger<StorefrontCatalogClient> logger
) : IStorefrontCatalogClient
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
        LogIfUnsuccessful(logger, request, response);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProductDto>>(ct) ?? [];
    }

    // Structured Warning at the service-to-service seam: a relayed-token rejection (401) vs. any other
    // non-success from Catalog. No auth header or token is logged — only method, path and status.
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
                "Catalog rejected relayed token: {Method} {RequestUri} returned 401",
                request.Method,
                request.RequestUri
            );
        }
        else
        {
            logger.LogWarning(
                "Downstream Catalog {Method} {RequestUri} returned {StatusCode}",
                request.Method,
                request.RequestUri,
                (int)response.StatusCode
            );
        }
    }
}
