using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        http.SendForJsonAsync<IReadOnlyList<ProductDto>>(
            HttpMethod.Get,
            "catalog/products",
            tokens,
            logger,
            ct: ct
        );

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        http.SendForJsonAsync<IReadOnlyList<CategoryDto>>(
            HttpMethod.Get,
            "catalog/categories",
            tokens,
            logger,
            ct: ct
        );

    public Task<(ProductDto? Product, string? Error)> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken ct = default
    ) => WriteAsync(HttpMethod.Post, "catalog/products", request, ct);

    public Task<(ProductDto? Product, string? Error)> UpdateProductAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken ct = default
    ) => WriteAsync(HttpMethod.Put, $"catalog/products/{id}", request, ct);

    // The write path keeps its own send loop (rather than TypedClientSendExtensions) because it
    // translates expected statuses into inline messages instead of throwing — but it preserves the
    // same ordering invariant: ThrowIfSessionExpired BEFORE any generic failure handling.
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
            HttpStatusCode.BadRequest => (null, await ReadBadRequestMessageAsync(response, ct)),
            HttpStatusCode.NotFound => (null, "That product no longer exists."),
            _ => throw new HttpRequestException(
                $"Catalog write failed ({(int)response.StatusCode})."
            ),
        };
    }

    // A 400 arrives in one of two shapes: the endpoint's own validation messages are a JSON string
    // (TypedResults.BadRequest("Unknown category '…'.")), while binding failures come back as RFC 7807
    // problem+json. Surface the human-readable message from either (detail over title for problem+json)
    // — never the raw JSON body — and fall back to a generic line for anything unrecognized.
    private static async Task<string> ReadBadRequestMessageAsync(
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var message = doc.RootElement.ValueKind switch
            {
                JsonValueKind.String => doc.RootElement.GetString(),
                JsonValueKind.Object => ProblemMessage(doc.RootElement),
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }
        catch (JsonException)
        {
            // Not JSON at all — fall through to the generic message.
        }
        return "The catalog rejected that product. Check the fields and try again.";
    }

    private static string? ProblemMessage(JsonElement problem)
    {
        if (
            problem.TryGetProperty("detail", out var detail)
            && detail.ValueKind == JsonValueKind.String
        )
        {
            return detail.GetString();
        }
        return
            problem.TryGetProperty("title", out var title)
            && title.ValueKind == JsonValueKind.String
            ? title.GetString()
            : null;
    }
}
