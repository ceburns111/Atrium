using System.ClientModel;
using Microsoft.Agents.AI.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// Wires the support agent and its config-driven <see cref="IChatClient"/>. The model provider is a
/// deployment concern, so it is selected entirely by configuration under <c>SupportAgent</c> — swapping
/// from the local fake to a real model is a config change, no code change.
/// </summary>
/// <remarks>
/// Config keys:
/// <list type="bullet">
///   <item><c>SupportAgent:Provider</c> — <c>Fake</c> | <c>FoundryLocal</c> | <c>AzureFoundry</c>.
///     Defaults to <c>Fake</c> in Development so the service boots with no AI config; in any other
///     environment a missing or unknown provider throws at startup.</item>
///   <item><c>SupportAgent:Endpoint</c>, <c>SupportAgent:ApiKey</c>, <c>SupportAgent:Model</c> — the
///     OpenAI-compatible endpoint, key, and deployment/model name used by the real providers. Foundry
///     Local and Azure AI Foundry share the same OpenAI-compatible client shape and differ only in
///     these values.</item>
/// </list>
/// </remarks>
public static class SupportAgentBuilderExtensions
{
    public static IHostApplicationBuilder AddSupportAgent(this IHostApplicationBuilder builder)
    {
        var chatClient = BuildChatClient(
            builder.Configuration,
            builder.Environment.IsDevelopment()
        );
        builder.Services.AddSingleton(chatClient);

        builder.Services.AddScoped<SupportTools>();

        // AG-UI serving support, plus the agent registered as a keyed AIAgent. MapAGUI captures ONE agent
        // instance at map time (resolved from the root provider), so the registration is Singleton and the
        // factory only depends on root-resolvable singletons (IChatClient, IHttpContextAccessor). The agent
        // resolves the request-scoped SupportTools per tool call from the caller's request scope (see
        // SupportAgent), so GetOrderStatus/FindProduct still answer for the signed-in user. The endpoint
        // (Program.cs) binds it by name via MapAGUI(AgentName, "/agent").
        builder.Services.AddAGUI();
        builder.Services.AddAIAgent(
            SupportAgent.AgentName,
            (serviceProvider, _) =>
                new SupportAgent(
                    serviceProvider.GetRequiredService<IChatClient>(),
                    serviceProvider.GetRequiredService<IHttpContextAccessor>()
                ).Agent,
            ServiceLifetime.Singleton
        );

        // Step-up MFA policy plumbing: config binding + the handler. The policy itself is registered on
        // the app's AuthorizationBuilder (Program.cs), alongside the "admin" policy.
        builder.Services.Configure<StepUpMfaOptions>(
            builder.Configuration.GetSection(StepUpMfaOptions.SectionName)
        );
        builder.Services.AddSingleton<IAuthorizationHandler, StepUpMfaHandler>();

        return builder;
    }

    private static IChatClient BuildChatClient(IConfiguration config, bool isDevelopment)
    {
        var provider = config["SupportAgent:Provider"];
        if (string.IsNullOrWhiteSpace(provider))
        {
            // The Development default is the in-service fake, so a fresh checkout boots (and the test
            // gate runs) with no model configured. Elsewhere, a missing provider is a misconfiguration.
            if (isDevelopment)
            {
                return new CannedChatClient();
            }

            throw new InvalidOperationException(
                "SupportAgent:Provider is not configured. Set it to 'Fake', 'Ollama', 'FoundryLocal', or "
                    + "'AzureFoundry' (the Fake default only applies in the Development environment)."
            );
        }

        // Foundry Local and Azure AI Foundry are both OpenAI-compatible; they differ only in the
        // configured endpoint/key/model, so they share one client-construction path.
        return provider.ToLowerInvariant() switch
        {
            "fake" => new CannedChatClient(),
            "ollama" => BuildOllamaClient(config),
            "foundrylocal" or "azurefoundry" => BuildOpenAICompatibleClient(config),
            _ => throw new InvalidOperationException(
                $"Unknown SupportAgent:Provider '{provider}'. Expected 'Fake', 'Ollama', 'FoundryLocal', or 'AzureFoundry'."
            ),
        };
    }

    // Ollama exposes an OpenAI-compatible API at /v1; the key is ignored but the SDK requires a non-empty value.
    private static IChatClient BuildOllamaClient(IConfiguration config)
    {
        var endpoint = config["SupportAgent:Endpoint"] ?? "http://localhost:11434/v1";
        var model = Require(config, "SupportAgent:Model");
        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
        );
        return client.GetChatClient(model).AsIChatClient();
    }

    // Test seam: exercise provider selection without standing up a host.
    internal static IChatClient BuildChatClientForTest(IConfiguration config, bool isDevelopment) =>
        BuildChatClient(config, isDevelopment);

    private static IChatClient BuildOpenAICompatibleClient(IConfiguration config)
    {
        var endpoint = Require(config, "SupportAgent:Endpoint");
        var apiKey = Require(config, "SupportAgent:ApiKey");
        var model = Require(config, "SupportAgent:Model");

        // Api-key auth from config (no DefaultAzureCredential): the cloud-credential story is deferred
        // with the rest of the Azure work.
        var client = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
        );
        return client.GetChatClient(model).AsIChatClient();
    }

    private static string Require(IConfiguration config, string key) =>
        config[key]
        ?? throw new InvalidOperationException(
            $"'{key}' must be configured for the selected SupportAgent provider."
        );

    /// <summary>
    /// Emits a startup warning when the step-up gate is inert outside Development — the gate is opt-in
    /// (<c>Enabled=false</c> by default), so a deploy that forgets <c>SupportAgent:StepUp:Enabled=true</c>
    /// silently downgrades <c>/storefront/agent</c> to authenticated-only with no other signal. In
    /// Development the gate is expected to be off, so nothing is logged there.
    /// </summary>
    public static void WarnIfStepUpGateInert(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            return;
        }

        var stepUp = app.Services.GetRequiredService<IOptions<StepUpMfaOptions>>().Value;
        if (!stepUp.Enabled)
        {
            app.Logger.LogWarning(
                "SupportAgent step-up MFA is DISABLED (SupportAgent:StepUp:Enabled=false) in {Environment}; "
                    + "the /storefront/agent endpoint is authenticated-only. Set Enabled=true to require step-up here.",
                app.Environment.EnvironmentName
            );
        }
    }
}
