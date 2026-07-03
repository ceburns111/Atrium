namespace Atrium.Services.Storefront.Support;

/// <summary>OTel source names for the Support agent — the single source of truth registered in Program.cs.</summary>
public static class SupportTelemetry
{
    /// <summary>Source for the Microsoft.Extensions.AI chat-client GenAI spans (model calls, tokens).</summary>
    public const string ChatSourceName = "Atrium.SupportAgent.Chat";

    /// <summary>Source for user-feedback spans (Phase 4).</summary>
    public const string FeedbackSourceName = "Atrium.Support.Feedback";

    /// <summary>
    /// MAF's default OTel source for agent-turn and tool-orchestration spans.
    /// Mirrors <c>Microsoft.Agents.AI.OpenTelemetryConsts.DefaultSourceName</c>, which is
    /// <c>internal</c> in Microsoft.Agents.AI 1.12.0 and cannot be referenced directly.
    /// Update here when upgrading MAF if the constant changes: if this string drifts from MAF's
    /// actual source name, agent-turn and tool-orchestration spans silently stop appearing in the
    /// Aspire dashboard (the tracer registers a source nothing emits under).
    /// </summary>
    public const string MafAgentSourceName = "Experimental.Microsoft.Agents.AI";
}
