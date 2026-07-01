using System.ClientModel;
using Microsoft.Extensions.AI;
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
public static class SupportAgentServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddSupportAgent(this IHostApplicationBuilder builder)
    {
        var chatClient = BuildChatClient(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(chatClient);

        builder.Services.AddScoped<SupportTools>();
        builder.Services.AddScoped<SupportAgent>();

        return builder;
    }

    private static IChatClient BuildChatClient(IConfiguration config, IHostEnvironment environment)
    {
        var provider = config["SupportAgent:Provider"];
        if (string.IsNullOrWhiteSpace(provider))
        {
            // The Development default is the in-service fake, so a fresh checkout boots (and the test
            // gate runs) with no model configured. Elsewhere, a missing provider is a misconfiguration.
            if (environment.IsDevelopment())
            {
                return new CannedChatClient();
            }

            throw new InvalidOperationException(
                "SupportAgent:Provider is not configured. Set it to 'Fake', 'FoundryLocal', or "
                    + "'AzureFoundry' (the Fake default only applies in the Development environment)."
            );
        }

        // Foundry Local and Azure AI Foundry are both OpenAI-compatible; they differ only in the
        // configured endpoint/key/model, so they share one client-construction path.
        return provider.ToLowerInvariant() switch
        {
            "fake" => new CannedChatClient(),
            "foundrylocal" or "azurefoundry" => BuildOpenAICompatibleClient(config),
            _ => throw new InvalidOperationException(
                $"Unknown SupportAgent:Provider '{provider}'. Expected 'Fake', 'FoundryLocal', or 'AzureFoundry'."
            ),
        };
    }

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
}
