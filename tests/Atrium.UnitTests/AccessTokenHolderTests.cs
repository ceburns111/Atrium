using System.Net;
using System.Text;
using System.Text.Json;
using Atrium.Design;
using Atrium.Modules.Storefront.Catalog;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atrium.UnitTests;

/// <summary>
/// U4b — a dead session must surface the SAME way for every signed-in request, not only for ones that
/// happen to hit an auth-gated endpoint. The reactive 401 path (see <see cref="SessionExpiredTests"/>)
/// misses reads bound for an <c>AllowAnonymous</c> endpoint — e.g. the Admin product list reads the
/// anonymous <c>catalog/products</c> (exercised here through the identical <c>CatalogClient</c> path),
/// so a stale token would get a 200 and hide the expiry while Reports (an admin-gated read) correctly
/// prompts a re-login. The holder now knows the token's own <c>exp</c> and the shared <c>Authorize</c>
/// step fails fast on it, so both behave identically.
/// </summary>
public class AccessTokenHolderTests
{
    [Fact]
    public void No_token_reads_as_not_expired()
    {
        var holder = new AccessTokenHolder();

        Assert.False(holder.IsExpired);
        Assert.Null(holder.ExpiresAt);
    }

    [Fact]
    public void A_token_with_a_future_exp_is_not_expired()
    {
        var holder = new AccessTokenHolder
        {
            AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(5)),
        };

        Assert.False(holder.IsExpired);
        Assert.NotNull(holder.ExpiresAt);
    }

    [Fact]
    public void A_token_whose_exp_has_passed_is_expired()
    {
        var holder = new AccessTokenHolder
        {
            AccessToken = Jwt(DateTimeOffset.UtcNow.AddSeconds(-1)),
        };

        Assert.True(holder.IsExpired);
    }

    [Fact]
    public void A_non_jwt_token_reads_as_not_expired_but_is_still_carried()
    {
        // Fail open: a token we can't parse must fall back to the reactive 401 path, never lock the
        // user out. The raw value is still attached so genuinely-authenticated calls keep working.
        var holder = new AccessTokenHolder { AccessToken = "not-a-jwt" };

        Assert.False(holder.IsExpired);
        Assert.Equal("not-a-jwt", holder.AccessToken);
    }

    [Fact]
    public void A_token_with_no_exp_claim_reads_as_not_expired()
    {
        var holder = new AccessTokenHolder { AccessToken = JwtWithPayload(new { sub = "user-1" }) };

        Assert.False(holder.IsExpired);
        Assert.Null(holder.ExpiresAt);
    }

    [Fact]
    public async Task An_anonymous_endpoint_read_with_an_expired_token_throws_before_the_server_answers()
    {
        // Directly models the reported bug: the Admin view reads catalog/products (AllowAnonymous),
        // which would answer 200 even to a dead session. The proactive check must short-circuit it so
        // the shell shows the "session expired" panel, exactly like Reports.
        var handler = new CountingHandler(HttpStatusCode.OK, "[]");
        var client = CatalogClient(handler, Jwt(DateTimeOffset.UtcNow.AddSeconds(-1)));

        await Assert.ThrowsAsync<SessionExpiredException>(() =>
            client.GetProductsAsync(ct: TestContext.Current.CancellationToken)
        );
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task A_live_token_read_still_reaches_the_server()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, "[]");
        var client = CatalogClient(handler, Jwt(DateTimeOffset.UtcNow.AddMinutes(5)));

        var products = await client.GetProductsAsync(ct: TestContext.Current.CancellationToken);

        Assert.Empty(products);
        Assert.Equal(1, handler.Calls);
    }

    private static CatalogClient CatalogClient(HttpMessageHandler handler, string token) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://gateway/") },
            new AccessTokenHolder { AccessToken = token },
            NullLogger<CatalogClient>.Instance
        );

    private static string Jwt(DateTimeOffset exp) =>
        JwtWithPayload(new { exp = exp.ToUnixTimeSeconds() });

    private static string JwtWithPayload(object payload)
    {
        static string Seg(object o) => Base64Url(JsonSerializer.SerializeToUtf8Bytes(o));
        return $"{Seg(new { alg = "none", typ = "JWT" })}.{Seg(payload)}.signature";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class CountingHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            return Task.FromResult(
                new HttpResponseMessage(status)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                }
            );
        }
    }
}
