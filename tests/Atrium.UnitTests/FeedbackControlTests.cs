using Atrium.Design;
using Atrium.UnitTests.Support;
using Bunit;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atrium.UnitTests;

/// <summary>
/// Pins the per-turn thumbs feedback control in <see cref="AgentChat"/>: clicking thumbs-down
/// must record <c>Value == -1</c> on the IFeedbackClient and flip the button to its active state.
/// </summary>
public class FeedbackControlTests
{
    [Fact]
    public void Thumbs_down_marks_the_turn_and_calls_the_client()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var sent = new List<int>();
        ctx.Services.AddSingleton<IFeedbackClient>(new FakeFeedbackClient(sent));
        ctx.Services.AddSingleton<IAgentChatClientFactory>(
            new FakeAgentChatClientFactory(new FakeChatClient("REAL ANSWER"))
        );
        ctx.Services.AddLogging();

        var cut = ctx.Render<AgentChat>(p =>
            p.Add(c => c.Endpoint, "storefront/agent").Add(c => c.StarterPrompts, new[] { "Hello" })
        );

        // Trigger a user send via the starter-prompt chip.
        cut.Find(".agent-chat__chip").Click();

        // Streaming is async — wait until the thumbs appear (assistant turn completed).
        cut.WaitForElement("[data-testid=fb-down]");

        // Click thumbs-down.
        cut.Find("[data-testid=fb-down]").Click();

        // The feedback client must have received Value == -1.
        Assert.Equal(-1, sent.Single());

        // The clicked button must carry the active state.
        Assert.Contains("is-active", cut.Find("[data-testid=fb-down]").GetAttribute("class") ?? "");
    }

    private sealed class FakeAgentChatClientFactory(IChatClient client) : IAgentChatClientFactory
    {
        public IChatClient Create(string endpoint) => client;
    }

    private sealed class FakeFeedbackClient(List<int> sent) : IFeedbackClient
    {
        public Task SendAsync(string endpoint, FeedbackDto dto, CancellationToken ct = default)
        {
            sent.Add(dto.Value);
            return Task.CompletedTask;
        }
    }
}
