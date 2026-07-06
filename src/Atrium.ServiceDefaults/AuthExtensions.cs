using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atrium.ServiceDefaults;

/// <summary>
/// Shared Keycloak JWT-bearer wiring for the backend services (Catalog, Storefront), so the
/// load-bearing claim-mapping settings live in exactly one place instead of drifting per host.
/// The Portal is deliberately not a caller — it authenticates with OIDC cookies, not bearer tokens.
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// Validates Keycloak-issued JWTs against the shared <c>atrium</c> realm and requires the shared
    /// <c>atrium</c> audience (stamped on every access token by the realm's audience mapper), then
    /// registers the <c>admin</c> authorization policy every service gates its back-office surface on.
    /// Returns the <see cref="AuthorizationBuilder"/> so a host can chain service-specific policies
    /// (Storefront adds the step-up MFA policy for the support agent). Requires service discovery to
    /// be registered so the JWKS backchannel can resolve <c>https+http://keycloak</c>.
    /// </summary>
    public static AuthorizationBuilder AddAtriumJwtAuth(this WebApplicationBuilder builder)
    {
        builder
            .Services.AddAuthentication()
            .AddKeycloakJwtBearer(
                "keycloak",
                realm: "atrium",
                options =>
                {
                    options.Audience = "atrium";
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    // Keep Keycloak's short claim names as-is; the legacy inbound map would otherwise
                    // rename the flat "role" claim to ClaimTypes.Role and defeat the RoleClaimType
                    // match below.
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.NameClaimType = "preferred_username";
                    // Keycloak's realm-role mapper flattens realm roles into a multivalued "role" claim.
                    options.TokenValidationParameters.RoleClaimType = "role";
                }
            );

        // The one cross-service policy: back-office writes/reads are gated on the admin realm role.
        return builder
            .Services.AddAuthorizationBuilder()
            .AddPolicy("admin", policy => policy.RequireRole("admin"));
    }
}
