using Atrium.Contracts;
using Atrium.Services.Storefront.Support;

namespace Atrium.UnitTests.Support;

/// <summary>
/// Proves the <see cref="SupportAgent"/> wires its two tools onto a <see cref="ChatClientAgent"/> and
/// runs a turn over a swappable <c>IChatClient</c> (the shared <see cref="FakeChatClient"/>), so the
/// agent construction — including tool registration — is exercised without a real model.
/// </summary>
public class SupportAgentTests
{
    [Fact]
    public async Task Agent_builds_with_its_tools_and_runs_a_turn()
    {
        using var chatClient = new FakeChatClient();
        var tools = new SupportTools(
            SupportTestDoubles.HttpContextFor("alice"),
            new FakeOrderRepository(),
            new FakeCatalogClient(new ProductDto(1, "Widget", "Tools", 9.99m, "A widget"))
        );
        var agent = new SupportAgent(chatClient, tools);

        var response = await agent.RunAsync(
            "hi",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Contains(FakeChatClient.CannedResponse, response.Text);
    }
}
