// FeedbackDto comes from Atrium.Contracts (global using in the csproj) — the one shared wire contract
// this client and the Storefront feedback endpoint both compile against (ADR-0006).
using System.Net.Http.Json;

namespace Atrium.Design;

/// <summary>Posts thumbs feedback to the gateway; the component calls this on each thumb click.</summary>
public interface IFeedbackClient
{
    Task SendAsync(string endpoint, FeedbackDto dto, CancellationToken ct = default);
}

/// <summary>
/// Posts thumbs feedback to the gateway, reusing the same authenticated gateway handler chain as
/// <see cref="AgentChatClientFactory"/> (service discovery + per-circuit bearer from
/// <see cref="AccessTokenHolder"/>). Builds the <see cref="HttpClient"/> inline rather than via
/// <see cref="IHttpClientFactory"/> because the bearer token must come from the circuit-scoped
/// <see cref="AccessTokenHolder"/> — the same reason <see cref="AgentChatClientFactory"/> does the
/// same. The feedback endpoint is <c>RequireAuthorization</c>, so an unauthenticated POST would 401.
/// </summary>
public sealed class FeedbackClient(
    IHttpMessageHandlerFactory handlerFactory,
    AccessTokenHolder tokens
) : IFeedbackClient
{
    public async Task SendAsync(string endpoint, FeedbackDto dto, CancellationToken ct = default)
    {
        var gatewayChain = handlerFactory.CreateHandler(AgentChatDefaults.HttpClientName);
        var bearer = new BearerTokenHandler(tokens) { InnerHandler = gatewayChain };
        using var http = new HttpClient(bearer, disposeHandler: false)
        {
            BaseAddress = new Uri(AgentChatDefaults.GatewayAddress),
        };
        using var response = await http.PostAsJsonAsync($"{endpoint}/feedback", dto, ct);
        response.EnsureSuccessStatusCode();
    }
}
