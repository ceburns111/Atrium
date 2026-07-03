using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Atrium.Services.Storefront.Support;

/// <summary>User feedback on an assistant turn. Telemetry-only: a span + a structured log, no persistence.
/// A thumbs-down turn is a candidate item for the eval dataset (the data flywheel).</summary>
public sealed record FeedbackRequest(string TurnId, int Value, string? Question, string? Answer);

public static class SupportFeedback
{
    private static readonly ActivitySource Source = new(
        SupportTelemetry.FeedbackSourceName,
        "1.0.0"
    );

    public static void Record(FeedbackRequest request, string user, ILogger? logger = null)
    {
        using var activity = Source.StartActivity("support.feedback");
        activity?.SetTag("feedback.turn_id", request.TurnId);
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
                (FeedbackRequest request, HttpContext http, ILoggerFactory lf) =>
                {
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
