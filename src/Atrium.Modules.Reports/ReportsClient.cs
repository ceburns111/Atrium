using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Atrium.Contracts;
using Atrium.Design;
using Microsoft.Extensions.Logging;

namespace Atrium.Modules.Reports;

/// <summary>
/// Typed client for the Storefront vertical's analytics endpoint, reached through the gateway. Attaches
/// the signed-in user's access token (from <see cref="AccessTokenHolder"/>) so the aggregate is read as
/// that user; the Storefront service composes Catalog to bucket sales by category.
/// </summary>
public sealed class ReportsClient(
    HttpClient http,
    AccessTokenHolder tokens,
    ILogger<ReportsClient> logger
)
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
        LogIfUnsuccessful(logger, request, response);
        response.ThrowIfSessionExpired();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SalesReportDto>(ct)
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
