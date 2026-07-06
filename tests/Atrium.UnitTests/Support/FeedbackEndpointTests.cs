using System.Diagnostics;
using Atrium.Contracts;
using Atrium.Services.Storefront.Support;

namespace Atrium.UnitTests.Support;

public class FeedbackEndpointTests
{
    [Fact]
    public void Recording_feedback_emits_a_span_with_the_thumb_value()
    {
        Activity? captured = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == SupportTelemetry.FeedbackSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = a => captured = a,
        };
        ActivitySource.AddActivityListener(listener);

        SupportFeedback.Record(
            new FeedbackDto("turn-1", -1, "where is my order", "It is confirmed."),
            "admin"
        );

        Assert.NotNull(captured);
        Assert.Equal("-1", captured!.GetTagItem("feedback.value")?.ToString());
        Assert.Equal("turn-1", captured.GetTagItem("feedback.turn_id")?.ToString());
    }
}
