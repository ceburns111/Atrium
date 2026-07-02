using Atrium.Services.Storefront.Support;
using Microsoft.AspNetCore.Http;

namespace Atrium.UnitTests.Support;

/// <summary>
/// Proves the <see cref="SupportAgent"/> wires its two tools onto a <see cref="ChatClientAgent"/> and
/// runs a turn over a swappable <c>IChatClient</c> (the shared <see cref="FakeChatClient"/>), so the
/// agent construction — including tool registration — is exercised without a real model. The agent is
/// built with only an <see cref="IHttpContextAccessor"/> (it resolves its scoped tools per call), so no
/// request scope is needed for this turn: the fake client replies without invoking any tool.
/// </summary>
public class SupportAgentTests
{
    [Fact]
    public async Task Agent_builds_with_its_tools_and_runs_a_turn()
    {
        using var chatClient = new FakeChatClient();
        var agent = new SupportAgent(chatClient, SupportTestDoubles.HttpContextFor("alice"));

        var response = await agent.RunAsync(
            "hi",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Contains(FakeChatClient.CannedResponse, response.Text);
    }
}
