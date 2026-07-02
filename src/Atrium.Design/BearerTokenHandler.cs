namespace Atrium.Design;

/// <summary>
/// Attaches the signed-in user's bearer token to every request an <c>AGUIChatClient</c> sends, and
/// turns a downstream <c>401</c> into the typed <see cref="SessionExpiredException"/> the chat surfaces
/// as a "sign in again" prompt. The AG-UI client owns its <see cref="HttpClient"/> internally, so a
/// module-style "attach the header in the typed client" call has no seam to hook — a
/// <see cref="DelegatingHandler"/> is the one place to authorize its traffic.
/// </summary>
/// <remarks>
/// Reuses the same <see cref="HttpRequestAuthorizationExtensions.Authorize"/> and
/// <see cref="HttpResponseSessionExtensions.ThrowIfSessionExpired"/> helpers as the module typed clients,
/// so token attachment and session-expiry detection are written once. It reads the token from the
/// per-circuit <see cref="AccessTokenHolder"/>, so it must be composed within the same scope the shell
/// populates (see <see cref="AgentChatServiceCollectionExtensions.AddAgentChat"/>).
/// </remarks>
public sealed class BearerTokenHandler(AccessTokenHolder tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        request.Authorize(tokens);
        var response = await base.SendAsync(request, cancellationToken);
        response.ThrowIfSessionExpired();
        return response;
    }
}
