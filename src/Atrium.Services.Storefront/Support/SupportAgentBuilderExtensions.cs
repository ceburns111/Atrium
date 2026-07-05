using System.ClientModel;
using Microsoft.Agents.AI.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
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
///   <item><c>SupportAgent:Provider</c> — <c>Fake</c> | <c>Ollama</c> | <c>FoundryLocal</c> |
///     <c>AzureFoundry</c>. <c>Ollama</c> is the flagship local provider (the AppHost configures it);
///     Provider defaults to <c>Fake</c> in Development so the service boots with no AI config; in any
///     other environment a missing or unknown provider throws at startup.</item>
///   <item><c>SupportAgent:Endpoint</c>, <c>SupportAgent:ApiKey</c>, <c>SupportAgent:Model</c> — the
///     OpenAI-compatible endpoint, key, and deployment/model name used by the real providers. Ollama
///     exposes the same OpenAI-compatible surface at <c>/v1</c> (no key needed); Foundry Local and
///     Azure AI Foundry differ only in these values.</item>
///   <item><c>SupportAgent:GuardrailModel</c> — the cheap classifier model for the input guardrail
///     (e.g. <c>llama3.2:3b</c>), served from the same Ollama endpoint. When unset the guardrail is a
///     permissive no-op (see <see cref="BuildGuardrailClassifier"/>).</item>
/// </list>
/// </remarks>
public static class SupportAgentBuilderExtensions
{
    // The one service-side default for the local Ollama daemon's OpenAI-compatible surface — used by
    // both the chat provider and the guardrail classifier when SupportAgent:Endpoint is unset.
    private const string DefaultOllamaEndpoint = "http://localhost:11434/v1";

    // Chat cache entries expire after this TTL; without one, DistributedCachingChatClient writes
    // never-expiring entries (see TtlDistributedCache).
    private static readonly TimeSpan ChatCacheTtl = TimeSpan.FromMinutes(15);

    public static IHostApplicationBuilder AddSupportAgent(this IHostApplicationBuilder builder)
    {
        // Register the raw provider client + the instrumented pipeline. Factory-based so later decorators
        // (cache, guardrail) can resolve their own dependencies from DI.
        builder.Services.AddDistributedMemoryCache();

        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var inner = BuildChatClient(builder.Configuration, builder.Environment.IsDevelopment());
            var cache = sp.GetRequiredService<IDistributedCache>();
            var classifier = BuildGuardrailClassifier(
                builder.Configuration,
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(nameof(SupportAgentBuilderExtensions))
            );
            return BuildSupportPipeline(inner, cache, classifier, sp);
        });

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
        var endpoint = config["SupportAgent:Endpoint"] ?? DefaultOllamaEndpoint;
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

    // The Support chat pipeline: OTel (#1 outermost) → guardrail (#4) → cache (#5 innermost).
    // ChatClientBuilder makes the FIRST-added decorator outermost (factories are built in reverse).
    // OTel outermost: every request — hits, misses, and blocks — is captured in a span.
    // Guardrail outside cache: a blocked request never warms the cache or reaches the model.
    // Cache innermost: only real model responses are cached.
    // Extracted so tests can drive the real pipeline construction with a controllable inner client.
    internal static IChatClient BuildSupportPipeline(
        IChatClient inner,
        IDistributedCache cache,
        IChatClient classifier,
        IServiceProvider services
    ) =>
        new ChatClientBuilder(inner)
            .UseOpenTelemetry(
                sourceName: SupportTelemetry.ChatSourceName,
                configure: o => o.EnableSensitiveData = true
            ) // #1 outermost — measures every request (hits, misses, blocks)
            .Use(
                (c, s) =>
                    new GuardrailChatClient(
                        c,
                        classifier,
                        s.GetService<ILoggerFactory>()?.CreateLogger<GuardrailChatClient>()
                    )
            ) // #4 outside cache — a block never warms cache or hits model
            // Cache safety note: entries are keyed on the FULL serialized transcript + options, with no
            // user partitioning. That is deliberate and currently safe: identical transcripts produce
            // identical answers, and any user-specific reply only exists downstream of a tool call whose
            // RESULT TEXT is part of the follow-up request (so the key), which keeps one user's order
            // data from ever answering another user's request. If a per-user input (claims, profile)
            // ever reaches the prompt outside the message list, add the user to the cache key.
            .UseDistributedCache(new TtlDistributedCache(cache, ChatCacheTtl)) // #5 innermost — caches only real model responses, TTL-bounded
            .Build(services);

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

    // Builds the cheap classifier client used by GuardrailChatClient. When no GuardrailModel is
    // configured (Fake / dev / unit tests) it returns a permissive canned classifier that always
    // answers "ALLOW", so the pipeline boots and tests pass with no Ollama instance running.
    // In production the AppHost sets SupportAgent__GuardrailModel=llama3.2:3b (Task 1.3).
    private static IChatClient BuildGuardrailClassifier(IConfiguration config, ILogger logger)
    {
        var model = config["SupportAgent:GuardrailModel"];
        if (string.IsNullOrWhiteSpace(model))
        {
            // A real model with no guardrail is the same silent-downgrade failure mode the step-up
            // gate warns about (WarnIfStepUpGateInert): everything still works, but every user message
            // reaches the model unscreened. Warn loudly instead of degrading silently.
            var provider = config["SupportAgent:Provider"];
            if (
                !string.IsNullOrWhiteSpace(provider)
                && !provider.Equals("fake", StringComparison.OrdinalIgnoreCase)
            )
            {
                logger.LogWarning(
                    "SupportAgent guardrail is INERT: provider '{Provider}' is configured but "
                        + "SupportAgent:GuardrailModel is not set, so every message is allowed through "
                        + "unscreened. Set SupportAgent:GuardrailModel (e.g. llama3.2:3b) to enable the "
                        + "input guardrail.",
                    provider
                );
            }

            // No guardrail model configured → permissive canned classifier (always ALLOW).
            return new CannedChatClient("ALLOW");
        }

        var endpoint = config["SupportAgent:Endpoint"] ?? DefaultOllamaEndpoint;
        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
        );
        // Instrumented under the same source as the chat pipeline so classifier calls appear as their
        // own GenAI spans in the Aspire trace (previously the guardrail's latency was invisible).
        return client
            .GetChatClient(model)
            .AsIChatClient()
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: SupportTelemetry.ChatSourceName,
                configure: o => o.EnableSensitiveData = true
            )
            .Build();
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
