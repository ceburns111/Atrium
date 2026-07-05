using Atrium.Contracts;
using Atrium.Design;
using Atrium.Services.Storefront.Support;
using Atrium.UnitTests.Support;
using Bunit;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atrium.UnitTests;

/// <summary>
/// Pins the per-turn thumbs feedback control in <see cref="AgentChat"/>: clicking thumbs-down
/// must record <c>Value == -1</c> on the IFeedbackClient and flip the button to its active state;
/// re-clicking the recorded value must not re-POST; and a failed POST must roll the optimistic
/// active state back so the vote can be retried.
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
            new FakeAgentChatClientFactory(new CannedChatClient("REAL ANSWER"))
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

    [Fact]
    public void Repeat_clicks_of_the_same_value_do_not_repost()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var sent = new List<int>();
        ctx.Services.AddSingleton<IFeedbackClient>(new FakeFeedbackClient(sent));
        ctx.Services.AddSingleton<IAgentChatClientFactory>(
            new FakeAgentChatClientFactory(new CannedChatClient("REAL ANSWER"))
        );
        ctx.Services.AddLogging();

        var cut = ctx.Render<AgentChat>(p =>
            p.Add(c => c.Endpoint, "storefront/agent").Add(c => c.StarterPrompts, new[] { "Hello" })
        );

        cut.Find(".agent-chat__chip").Click();
        cut.WaitForElement("[data-testid=fb-down]");

        // Same vote twice — the second click is a no-op, not a duplicate POST.
        cut.Find("[data-testid=fb-down]").Click();
        cut.Find("[data-testid=fb-down]").Click();

        Assert.Equal(-1, sent.Single());

        // Changing the vote IS a new POST.
        cut.Find("[data-testid=fb-up]").Click();
        Assert.Equal(new[] { -1, 1 }, sent);
    }

    [Fact]
    public void Failed_post_rolls_back_the_optimistic_active_state()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.AddSingleton<IFeedbackClient>(new ThrowingFeedbackClient());
        ctx.Services.AddSingleton<IAgentChatClientFactory>(
            new FakeAgentChatClientFactory(new CannedChatClient("REAL ANSWER"))
        );
        ctx.Services.AddLogging();

        var cut = ctx.Render<AgentChat>(p =>
            p.Add(c => c.Endpoint, "storefront/agent").Add(c => c.StarterPrompts, new[] { "Hello" })
        );

        cut.Find(".agent-chat__chip").Click();
        cut.WaitForElement("[data-testid=fb-down]");

        cut.Find("[data-testid=fb-down]").Click();

        // The POST failed, so the optimistic flip must have been rolled back on both buttons.
        Assert.DoesNotContain(
            "is-active",
            cut.Find("[data-testid=fb-down]").GetAttribute("class") ?? ""
        );
        Assert.DoesNotContain(
            "is-active",
            cut.Find("[data-testid=fb-up]").GetAttribute("class") ?? ""
        );
    }

    private sealed class FakeAgentChatClientFactory(IChatClient client) : IAgentChatClientFactory
    {
        public IChatClient Create(string endpoint) => client;
    }

    private sealed class ThrowingFeedbackClient : IFeedbackClient
    {
        public Task SendAsync(string endpoint, FeedbackDto dto, CancellationToken ct = default) =>
            throw new HttpRequestException("boom");
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
