using System.Net;
using Atrium.Design;

namespace Atrium.UnitTests.Support;

/// <summary>
/// Deterministic, host-free coverage of <see cref="BearerTokenHandler"/> — the one seam that authorizes
/// the AG-UI chat's traffic (the AGUIChatClient owns its HttpClient internally). Asserts it attaches the
/// circuit's bearer token, leaves the header off when there is no token, and maps a downstream 401 to the
/// typed <see cref="SessionExpiredException"/> the chat surfaces as a "sign in again" prompt.
/// </summary>
public class BearerTokenHandlerTests
{
    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Received { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Received = request;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private static (HttpMessageInvoker Invoker, CapturingHandler Inner) Build(
        string? token,
        HttpStatusCode status = HttpStatusCode.OK
    )
    {
        var inner = new CapturingHandler(status);
        var handler = new BearerTokenHandler(new AccessTokenHolder { AccessToken = token })
        {
            InnerHandler = inner,
        };
        return (new HttpMessageInvoker(handler), inner);
    }

    private static HttpRequestMessage Request() =>
        new(HttpMethod.Post, "https+http://gateway/storefront/agent");

    [Fact]
    public async Task Attaches_bearer_token_from_the_holder()
    {
        var (invoker, inner) = Build("abc123");

        using var response = await invoker.SendAsync(
            Request(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Bearer", inner.Received!.Headers.Authorization!.Scheme);
        Assert.Equal("abc123", inner.Received.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Leaves_authorization_unset_when_there_is_no_token()
    {
        var (invoker, inner) = Build(token: null);

        using var response = await invoker.SendAsync(
            Request(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(inner.Received!.Headers.Authorization);
    }

    [Fact]
    public async Task Maps_401_to_session_expired()
    {
        var (invoker, _) = Build("abc123", HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<SessionExpiredException>(() =>
            invoker.SendAsync(Request(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Passes_a_successful_response_through()
    {
        var (invoker, _) = Build("abc123");

        using var response = await invoker.SendAsync(
            Request(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
