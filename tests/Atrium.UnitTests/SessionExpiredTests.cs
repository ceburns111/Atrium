using System.Net;
using Atrium.Design;
using Atrium.Modules.Storefront.Catalog;

namespace Atrium.UnitTests;

/// <summary>
/// U4 — an expired access token (a 401 from the Catalog core, since this demo has no token refresh)
/// must surface as a typed <see cref="SessionExpiredException"/> the shell can turn into a "sign in
/// again" prompt — not a raw <see cref="HttpRequestException"/> that tears down the Blazor circuit.
/// Concept on show: mapping a transport status code to a domain-meaningful, recoverable signal.
/// </summary>
public class SessionExpiredTests
{
    [Fact]
    public async Task Read_translates_401_into_a_SessionExpiredException()
    {
        var client = CatalogClientReturning(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<SessionExpiredException>(() => client.GetProductsAsync());
    }

    [Fact]
    public async Task Read_still_throws_for_other_failures()
    {
        var client = CatalogClientReturning(HttpStatusCode.InternalServerError);

        // A 500 is a genuine fault, not an expired session — it must not be masked as a session prompt.
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetProductsAsync());
    }

    private static CatalogClient CatalogClientReturning(HttpStatusCode status)
    {
        var http = new HttpClient(new StubHandler(status))
        {
            BaseAddress = new Uri("https://gateway/"),
        };
        return new CatalogClient(http, new AccessTokenHolder { AccessToken = "expired-token" });
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(status));
    }
}
