using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

namespace Atrium.Design;

/// <summary>
/// Testability seam: lets unit tests inject a fake factory without depending on the sealed concrete type.
/// </summary>
public interface IAgentChatClientFactory
{
    /// <summary>Creates a chat client for a gateway-relative agent endpoint (e.g. <c>"storefront/agent"</c>).</summary>
    IChatClient Create(string endpoint);
}

/// <summary>
/// Builds the AG-UI <see cref="IChatClient"/> the chat talks to, inside the signed-in Blazor circuit.
/// It takes the gateway handler chain from <see cref="IHttpMessageHandlerFactory"/> (so service
/// discovery and telemetry apply) and wraps it with a <see cref="BearerTokenHandler"/> constructed from
/// this circuit's <see cref="AccessTokenHolder"/> — the token flows correctly because the wrapping
/// happens here in the circuit scope, not in the factory's separate handler scope.
/// </summary>
public sealed class AgentChatClientFactory(
    IHttpMessageHandlerFactory handlerFactory,
    AccessTokenHolder tokens
) : IAgentChatClientFactory
{
    /// <summary>
    /// Creates a chat client for a gateway-relative agent endpoint (e.g. <c>"storefront/agent"</c>).
    /// The caller owns the result and must dispose it when the chat tears down.
    /// </summary>
    public IChatClient Create(string endpoint)
    {
        // The named client's pooled chain (service discovery → primary handler); owned by the factory,
        // so our HttpClient must not dispose it.
        var gatewayChain = handlerFactory.CreateHandler(AgentChatDefaults.HttpClientName);
        var bearer = new BearerTokenHandler(tokens) { InnerHandler = gatewayChain };
        // disposeHandler:false — a DelegatingHandler.Dispose() cascades to its inner handler, and the
        // gateway chain is pooled/owned by IHttpMessageHandlerFactory. The thin bearer holds no
        // resources, so leaving it undisposed is harmless; disposing the pooled chain would not be.
        var http = new HttpClient(bearer, disposeHandler: false)
        {
            BaseAddress = new Uri(AgentChatDefaults.GatewayAddress),
        };
        return new AGUIChatClient(http, endpoint);
    }
}
