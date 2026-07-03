using Atrium.Services.Storefront.Support;
using Microsoft.Extensions.Configuration;

namespace Atrium.UnitTests.Support;

public class SupportProviderTests
{
    [Fact]
    public void Ollama_provider_builds_a_chat_client_from_config()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SupportAgent:Provider"] = "Ollama",
                    ["SupportAgent:Model"] = "qwen2.5:7b-instruct",
                }
            )
            .Build();

        var client = SupportAgentBuilderExtensions.BuildChatClientForTest(
            config,
            isDevelopment: true
        );

        Assert.NotNull(client);
    }

    [Fact]
    public void Unknown_provider_throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["SupportAgent:Provider"] = "Nope" }
            )
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            SupportAgentBuilderExtensions.BuildChatClientForTest(config, isDevelopment: true)
        );
    }
}
