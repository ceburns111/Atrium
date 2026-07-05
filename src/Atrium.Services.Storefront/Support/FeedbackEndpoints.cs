using System.Diagnostics;
using Atrium.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Storefront.Support;

/// <summary>User feedback on an assistant turn (the shared <see cref="FeedbackDto"/> wire contract).
/// Telemetry-only: a span + a structured log, no persistence. A thumbs-down turn is a candidate item
/// for the eval dataset (the data flywheel).</summary>
public static class SupportFeedback
{
    private static readonly ActivitySource Source = new(
        SupportTelemetry.FeedbackSourceName,
        "1.0.0"
    );

    public static void Record(FeedbackDto request, string user, ILogger? logger = null)
    {
        // Known limitation: this span is a root activity keyed only by the client-generated turn id —
        // it cannot be joined to the originating chat trace from recorded data. Correlating would need
        // the browser to echo the chat run's traceparent back with the feedback POST, and the AG-UI
        // client/AgentChat component expose no seam for that today. Filter on feedback.turn_id +
        // feedback.user + time window to line feedback up with chat spans in the Aspire dashboard.
        using var activity = Source.StartActivity("support.feedback");
        activity?.SetTag("feedback.turn_id", Truncate(request.TurnId));
        activity?.SetTag("feedback.value", request.Value); // +1 up, -1 down
        activity?.SetTag("feedback.user", user);
        activity?.SetTag("feedback.question", Truncate(request.Question));
        activity?.SetTag("feedback.answer", Truncate(request.Answer));

        logger?.LogInformation(
            "Support feedback {Value} from {User} on turn {TurnId}",
            request.Value,
            user,
            request.TurnId
        );
    }

    private static string? Truncate(string? s) => s is { Length: > 500 } ? s[..500] : s;

    public static void MapSupportFeedback(this IEndpointRouteBuilder storefront)
    {
        storefront
            .MapPost(
                "/agent/feedback",
                (FeedbackDto request, HttpContext http, ILoggerFactory lf) =>
                {
                    if (request.Value is not (1 or -1))
                    {
                        return Results.BadRequest();
                    }

                    SupportFeedback.Record(
                        request,
                        http.User.Identity?.Name ?? "unknown",
                        lf.CreateLogger("SupportFeedback")
                    );
                    return Results.NoContent();
                }
            )
            .RequireAuthorization()
            .WithTags("Support");
    }
}
