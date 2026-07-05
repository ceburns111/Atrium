using Atrium.Services.Storefront.Support;
using Microsoft.Agents.AI;

namespace Atrium.UnitTests;

/// <summary>
/// GO/NO-GO smoke proof that the Microsoft Agent Framework stack restores, compiles, and runs an
/// agent turn over a swappable <c>IChatClient</c> on this repo's .NET 10 setup. The real model is
/// replaced by the service's own <see cref="CannedChatClient"/> (the Fake provider) so the test is
/// deterministic and offline. This is the seed the later tool-calling tests grow from.
/// </summary>
public class MafAgentSmokeTests
{
    private const string CannedReply = "PONG from the fake model.";

    [Fact]
    public async Task Agent_over_fake_chat_client_returns_the_canned_text()
    {
        // A ChatClientAgent is the MAF agent that wraps any IChatClient. (The 1.12.0 API exposes
        // this ctor directly; the docs' `IChatClient.CreateAIAgent(...)` extension does not exist
        // in this stable release.)
        using var chatClient = new CannedChatClient(CannedReply);
        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are the Atrium storefront support agent.",
            name: "Support"
        );

        // RunAsync(string) drives one turn and returns an AgentResponse whose .Text is the reply.
        AgentResponse response = await agent.RunAsync(
            "ping",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Contains(CannedReply, response.Text);
    }
}
