using System.Net.Http.Headers;
using System.Net.Http.Json;
using Atrium.Contracts;
using Atrium.Design;

namespace Atrium.Modules.Reports;

/// <summary>
/// Typed client for the Storefront vertical's analytics endpoint, reached through the gateway. Attaches
/// the signed-in user's access token (from <see cref="AccessTokenHolder"/>) so the aggregate is read as
/// that user; the Storefront service composes Catalog to bucket sales by category.
/// </summary>
public sealed class ReportsClient(HttpClient http, AccessTokenHolder tokens)
{
    public async Task<SalesReportDto> GetSalesAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "storefront/reports/sales");
        if (!string.IsNullOrEmpty(tokens.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokens.AccessToken
            );
        }
        using var response = await http.SendAsync(request, ct);
        response.ThrowIfSessionExpired();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SalesReportDto>(ct)
            ?? throw new InvalidOperationException();
    }
}
