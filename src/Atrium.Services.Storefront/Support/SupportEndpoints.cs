using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// The support agent's HTTP surface: the AG-UI SSE endpoint at <c>/storefront/agent</c>. Mapped onto the
/// parent <c>/storefront</c> group (like <c>OrdersEndpoints</c>/<c>ReportsEndpoints</c>) so this owns only
/// <c>/agent</c>, then gated by the stronger step-up-MFA policy.
/// </summary>
public static class SupportEndpoints
{
    // Mapped onto the parent "/storefront" group. MapAGUI captures the keyed AIAgent once (a singleton),
    // but each tool call resolves a fresh request-scoped SupportTools, so the tools still see the signed-in
    // caller and that caller's scoped services. The gateway's existing /storefront/{**catch-all} route
    // already proxies this (YARP forwards SSE) — no gateway change is needed. The stronger StepUpMfa policy
    // overrides the group's authenticated-only default (never anonymous). No AgentSessionStore is
    // registered, so AG-UI threads are ephemeral (no cross-user ThreadId resume risk).
    public static void MapSupportAgent(this IEndpointRouteBuilder storefront)
    {
        storefront
            .MapAGUI(SupportAgent.AgentName, "/agent")
            .RequireAuthorization(StepUpMfaRequirement.PolicyName)
            .WithTags("Support");
    }
}
