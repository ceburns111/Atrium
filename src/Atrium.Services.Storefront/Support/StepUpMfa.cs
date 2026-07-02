using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// Config for the <c>StepUpMfa</c> authorization policy, bound from <c>SupportAgent:StepUp</c>. The
/// same policy works locally and in the cloud — only these values change:
/// <list type="bullet">
///   <item><see cref="Enabled"/> — master switch. <c>false</c> (the default) means the policy requires
///     only an authenticated user, so local browsing/dev is never blocked by a step-up ceremony.</item>
///   <item><see cref="Simulate"/> — the dev escape hatch, honored <b>only in the Development
///     environment</b>. When <see cref="Enabled"/> is <c>true</c>, this is <c>true</c>, and the host is
///     Development, any authenticated user is treated as stepped-up, so the gated path can be exercised
///     locally without a real MFA ceremony. Outside Development it is ignored (a real step-up claim is
///     still required), so a stray <c>Simulate=true</c> in a deployed config cannot weaken the gate.</item>
///   <item><see cref="AcceptedAmrValues"/> / <see cref="AcceptedAcrValues"/> — when
///     <see cref="Enabled"/> is <c>true</c> and <see cref="Simulate"/> is <c>false</c>, a real step-up
///     claim is required: an <c>amr</c> claim in the accepted set (what Entra stamps for MFA) OR an
///     <c>acr</c> claim in the accepted set (what a Keycloak step-up flow stamps). Both sets are
///     overridable via config.</item>
/// </list>
/// </summary>
public sealed class StepUpMfaOptions
{
    /// <summary>The config section this binds from.</summary>
    public const string SectionName = "SupportAgent:StepUp";

    public bool Enabled { get; set; }

    public bool Simulate { get; set; }

    /// <summary>Authentication-method (<c>amr</c>) values that count as a step-up (case-insensitive).</summary>
    public string[] AcceptedAmrValues { get; set; } = ["mfa", "otp", "hwk", "sms"];

    /// <summary>Authentication-context (<c>acr</c>) values that count as a step-up (case-insensitive).</summary>
    public string[] AcceptedAcrValues { get; set; } = ["mfa"];
}

/// <summary>The requirement carried by the <c>StepUpMfa</c> policy; <see cref="StepUpMfaHandler"/> evaluates it.</summary>
public sealed class StepUpMfaRequirement : IAuthorizationRequirement
{
    /// <summary>The policy name registered in DI and applied to the AG-UI endpoint.</summary>
    public const string PolicyName = "StepUpMfa";

    /// <summary>
    /// Builds the policy: always require an authenticated user first (so an anonymous caller is
    /// challenged with 401), then layer the step-up requirement on top (an authenticated caller that
    /// fails it is forbidden with 403).
    /// </summary>
    public static void Configure(AuthorizationPolicyBuilder policy)
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new StepUpMfaRequirement());
    }
}

/// <summary>
/// Evaluates <see cref="StepUpMfaRequirement"/> against the current principal using
/// <see cref="StepUpMfaOptions"/>. The claim checks are the cloud/local seam: an Entra token satisfies
/// them via <c>amr</c>, a Keycloak step-up flow via <c>acr</c> — the handler is identical either way.
/// </summary>
public sealed class StepUpMfaHandler(
    IOptions<StepUpMfaOptions> options,
    IHostEnvironment environment
) : AuthorizationHandler<StepUpMfaRequirement>
{
    private readonly StepUpMfaOptions _options = options.Value;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StepUpMfaRequirement requirement
    )
    {
        // Authentication is the policy's first requirement; if the caller isn't authenticated, leave the
        // requirement unmet so the pipeline challenges (401) rather than forbids (403).
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Disabled (the default): an authenticated caller is enough, no step-up claim. Simulate is a
        // DEVELOPMENT-ONLY escape hatch — honored only in the Development environment so a stray
        // `Simulate=true` in a deployed config can never silently bypass the real step-up ceremony.
        if (!_options.Enabled || (_options.Simulate && environment.IsDevelopment()))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Enabled and not simulated: require a real step-up claim.
        if (HasStepUpClaim(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private bool HasStepUpClaim(ClaimsPrincipal user)
    {
        // amr is multivalued: a JSON array in the token maps to several "amr" claims (one per method).
        var stepUpByAmr = user.FindAll("amr")
            .Any(c =>
                _options.AcceptedAmrValues.Contains(c.Value, StringComparer.OrdinalIgnoreCase)
            );

        var stepUpByAcr = user.FindAll("acr")
            .Any(c =>
                _options.AcceptedAcrValues.Contains(c.Value, StringComparer.OrdinalIgnoreCase)
            );

        return stepUpByAmr || stepUpByAcr;
    }
}
