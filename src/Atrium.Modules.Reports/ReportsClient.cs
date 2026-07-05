using Atrium.Contracts;
using Atrium.Design;
using Microsoft.Extensions.Logging;

namespace Atrium.Modules.Reports;

/// <summary>
/// Typed client for the Storefront vertical's analytics endpoint, reached through the gateway. Attaches
/// the signed-in user's access token (from <see cref="AccessTokenHolder"/>) so the aggregate is read as
/// that user; the Storefront service composes Catalog to bucket sales by category. The call rides the
/// shared <see cref="TypedClientSendExtensions"/> pipeline.
/// </summary>
public sealed class ReportsClient(
    HttpClient http,
    AccessTokenHolder tokens,
    ILogger<ReportsClient> logger
)
{
    public Task<SalesReportDto> GetSalesAsync(CancellationToken ct = default) =>
        http.SendForJsonAsync<SalesReportDto>(
            HttpMethod.Get,
            "storefront/reports/sales",
            tokens,
            logger,
            ct: ct
        );
}
