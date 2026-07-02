using System.Security.Claims;
using Atrium.Services.Storefront.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Atrium.UnitTests.Support;

/// <summary>
/// Deterministic, host-free coverage of the <c>StepUpMfa</c> policy logic: it constructs
/// <see cref="ClaimsPrincipal"/>s and an <see cref="AuthorizationHandlerContext"/> directly and asserts
/// <see cref="StepUpMfaHandler"/> succeeds or leaves the requirement unmet. These are the authoritative
/// tests for the Enabled/Simulate/claim matrix; the integration test only proves the endpoint is mapped
/// and non-anonymous.
/// </summary>
public class StepUpMfaHandlerTests
{
    // Defaults to the Development environment (the common case); the Simulate-guard tests pass an
    // explicit environment name to exercise the Development-only escape hatch.
    private static async Task<bool> EvaluateAsync(
        StepUpMfaOptions options,
        ClaimsPrincipal user,
        string environmentName = "Development"
    )
    {
        var environment = new FakeHostEnvironment(environmentName);
        var handler = new StepUpMfaHandler(Options.Create(options), environment);
        var requirement = new StepUpMfaRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    // An identity with no authenticationType is treated as unauthenticated by ClaimsIdentity.
    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    [Fact]
    public async Task Disabled_authenticated_user_succeeds_without_step_up_claim()
    {
        var succeeded = await EvaluateAsync(
            new StepUpMfaOptions { Enabled = false },
            Authenticated(new Claim("preferred_username", "alice"))
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task Enabled_and_simulated_in_development_succeeds_without_step_up_claim()
    {
        var succeeded = await EvaluateAsync(
            new StepUpMfaOptions { Enabled = true, Simulate = true },
            Authenticated(new Claim("preferred_username", "alice")),
            environmentName: Environments.Development
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task Simulate_is_ignored_outside_development_so_a_stray_prod_flag_cannot_bypass()
    {
        // Simulate is a Development-only escape hatch: in Production it must NOT treat an authenticated
        // user as stepped-up — a real step-up claim is still required.
        var succeeded = await EvaluateAsync(
            new StepUpMfaOptions { Enabled = true, Simulate = true },
            Authenticated(new Claim("preferred_username", "alice")),
            environmentName: Environments.Production
        );

        Assert.False(succeeded);
    }

    [Theory]
    [InlineData("mfa")]
    [InlineData("otp")]
    public async Task Enabled_not_simulated_user_with_accepted_amr_succeeds(string amr)
    {
        var succeeded = await EvaluateAsync(
            new StepUpMfaOptions { Enabled = true, Simulate = false },
            Authenticated(new Claim("preferred_username", "alice"), new Claim("amr", amr))
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task Enabled_not_simulated_user_without_step_up_claim_fails()
    {
        var succeeded = await EvaluateAsync(
            new StepUpMfaOptions { Enabled = true, Simulate = false },
            Authenticated(new Claim("preferred_username", "alice"))
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task Enabled_not_simulated_user_with_unaccepted_amr_fails()
    {
        var succeeded = await EvaluateAsync(
            new StepUpMfaOptions { Enabled = true, Simulate = false },
            Authenticated(new Claim("preferred_username", "alice"), new Claim("amr", "pwd"))
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task Enabled_not_simulated_user_with_accepted_acr_succeeds()
    {
        // Keycloak's step-up path stamps acr rather than amr; the same policy accepts it.
        var succeeded = await EvaluateAsync(
            new StepUpMfaOptions { Enabled = true, Simulate = false },
            Authenticated(new Claim("preferred_username", "alice"), new Claim("acr", "mfa"))
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task Accepted_amr_set_is_config_overridable()
    {
        var options = new StepUpMfaOptions
        {
            Enabled = true,
            Simulate = false,
            AcceptedAmrValues = ["fido2"],
        };

        Assert.True(
            await EvaluateAsync(
                options,
                Authenticated(new Claim("preferred_username", "a"), new Claim("amr", "fido2"))
            )
        );
        // A value from the default set is no longer accepted once the set is overridden.
        Assert.False(
            await EvaluateAsync(
                options,
                Authenticated(new Claim("preferred_username", "a"), new Claim("amr", "mfa"))
            )
        );
    }

    [Fact]
    public async Task Unauthenticated_user_never_succeeds_even_when_disabled()
    {
        // Enabled=false still requires authentication; an anonymous principal leaves the requirement
        // unmet so the pipeline challenges (401) rather than forbidding (403).
        var succeeded = await EvaluateAsync(new StepUpMfaOptions { Enabled = false }, Anonymous());

        Assert.False(succeeded);
    }

    // Minimal IHostEnvironment so the handler's Development-only Simulate guard can be exercised without
    // a host. Only EnvironmentName is consulted (via IsDevelopment()); the rest are inert defaults.
    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
