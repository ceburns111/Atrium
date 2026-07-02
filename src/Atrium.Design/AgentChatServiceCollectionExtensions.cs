using Microsoft.Extensions.DependencyInjection;

namespace Atrium.Design;

/// <summary>
/// Shared names for the AG-UI chat plumbing, so the registration, the handler chain, and any diagnostics
/// all refer to the one HttpClient by the same constant.
/// </summary>
public static class AgentChatDefaults
{
    /// <summary>Named <see cref="HttpClient"/> the <see cref="AgentChatClientFactory"/> builds on.</summary>
    public const string HttpClientName = "Atrium.AgentChat";

    /// <summary>
    /// Logical gateway address the modules already use; service discovery (added by the host's
    /// <c>ConfigureHttpClientDefaults</c>) resolves it to the running gateway.
    /// </summary>
    public const string GatewayAddress = "https+http://gateway";
}

/// <summary>
/// Registers the AG-UI chat plumbing the <c>AgentChat</c> component needs: the bearer
/// <see cref="BearerTokenHandler"/> and a named gateway <see cref="HttpClient"/>, plus the scoped
/// <see cref="AgentChatClientFactory"/> that composes them into an <c>AGUIChatClient</c> inside the
/// signed-in circuit. The host (Portal, item C5) calls this once and supplies the signed-in
/// <see cref="AccessTokenHolder"/> and service discovery it already registers for its module clients.
/// </summary>
public static class AgentChatServiceCollectionExtensions
{
    public static IServiceCollection AddAgentChat(this IServiceCollection services)
    {
        // The named client exists so its handler chain picks up the host's ConfigureHttpClientDefaults
        // (service discovery, telemetry). The bearer is deliberately NOT added here: it must read the
        // per-circuit AccessTokenHolder, and IHttpClientFactory builds handlers in a separate scope
        // where that holder is empty (see AccessTokenHolder). AgentChatClientFactory wraps the chain
        // with a bearer built from the circuit's holder instead.
        services.AddHttpClient(
            AgentChatDefaults.HttpClientName,
            client => client.BaseAddress = new Uri(AgentChatDefaults.GatewayAddress)
        );

        services.AddScoped<AgentChatClientFactory>();
        return services;
    }
}
