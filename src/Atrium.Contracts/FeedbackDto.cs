namespace Atrium.Contracts;

/// <summary>
/// User thumbs feedback on a single assistant chat turn, posted to the support agent's
/// <c>/agent/feedback</c> endpoint. <see cref="Value"/> is +1 (helpful) or -1 (not helpful).
/// Telemetry-only on the service side: recorded as a span + structured log, never persisted.
/// </summary>
public sealed record FeedbackDto(string TurnId, int Value, string? Question, string? Answer);
