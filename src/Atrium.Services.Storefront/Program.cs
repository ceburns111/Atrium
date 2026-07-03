using Atrium.ServiceDefaults;
using Atrium.Services.Storefront.Catalog;
using Atrium.Services.Storefront.Data;
using Atrium.Services.Storefront.Orders;
using Atrium.Services.Storefront.Reports;
using Atrium.Services.Storefront.Support;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging + OpenTelemetry tracing (ASP.NET Core, HttpClient, SqlClient) exported
// over OTLP to the Aspire dashboard. SqlClient is on because this vertical owns a database.
builder.AddAtriumTelemetry(instrumentSqlClient: true);

// GenAI spans: the chat-client pipeline (tokens/model) + the MAF agent (turns/tools) + feedback (Phase 4).
builder.Services.ConfigureOpenTelemetryTracerProvider(t =>
    t.AddSource(SupportTelemetry.ChatSourceName)
        .AddSource(SupportTelemetry.FeedbackSourceName)
        .AddSource(SupportTelemetry.MafAgentSourceName)
);
builder.Services.ConfigureOpenTelemetryMeterProvider(m =>
    m.AddMeter(SupportTelemetry.ChatSourceName)
);

// This vertical's own database (no EF; Dapper over sprocs), plus the caller's HttpContext for token relay.
builder.AddSqlServerClient("storefrontdb");
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

// Built-in OpenAPI (Microsoft.AspNetCore.OpenApi): the document is served at /openapi/v1.json and
// picks up each endpoint group's .WithTags(...) as OpenAPI tags. Registration is harmless in any
// environment; the document + docs page are only *exposed* in Development (see below).
builder.Services.AddOpenApi();

builder.Services.AddServiceDiscovery();
builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());

// Service-to-service call to the Catalog core (relays the caller's bearer token).
builder.Services.AddHttpClient<IStorefrontCatalogClient, StorefrontCatalogClient>(client =>
    client.BaseAddress = new Uri("https+http://catalog")
);

// MAF order-support agent + its config-driven IChatClient (Fake in Development; FoundryLocal/AzureFoundry
// via SupportAgent:* config). The AG-UI endpoint + gateway route are a later item; this only registers it.
builder.AddSupportAgent();

// Validate Keycloak JWTs; the shared "atrium" audience is stamped on every access token by the realm.
builder
    .Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        "keycloak",
        realm: "atrium",
        options =>
        {
            options.Audience = "atrium";
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            // Keep Keycloak's short claim names as-is; the legacy inbound map would otherwise rename
            // the flat "role" claim to ClaimTypes.Role and defeat the RoleClaimType match below.
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "preferred_username";
            // Keycloak's realm-role mapper flattens realm roles into a multivalued "role" claim.
            options.TokenValidationParameters.RoleClaimType = "role";
        }
    );

// Most of this vertical only needs an authenticated caller (relayed bearer). Reports is the exception:
// the analytics surface is admin-only, matching the admin-gated Reports page/nav in the portal.
builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy("admin", policy => policy.RequireRole("admin"))
    // Step-up MFA for the support agent endpoint: always authenticated, and (when enabled via
    // SupportAgent:StepUp) a real or simulated step-up claim. See StepUpMfa.cs.
    .AddPolicy(StepUpMfaRequirement.PolicyName, StepUpMfaRequirement.Configure);

var app = builder.Build();

var connectionString =
    app.Configuration.GetConnectionString("storefrontdb")
    ?? throw new InvalidOperationException("Connection string 'storefrontdb' was not configured.");
DatabaseInitializer.Initialize(connectionString, app.Logger);

// Surface a misconfigured (inert) step-up gate outside Development, where it is opt-in by default.
app.WarnIfStepUpGateInert();

// One structured log event per request (method, path, status, elapsed); early so it wraps handlers.
app.UseAtriumRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// API docs, Development-only and anonymous: both routes are mapped at the app root (outside the
// bearer-only "/storefront" group) and AllowAnonymous, so the morning live-check can reach them
// without a token. /openapi/v1.json is the raw document; /docs renders it with Redoc (standalone).
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapGet(
            "/docs",
            () =>
                Results.Content(
                    """
                    <!DOCTYPE html>
                    <html>
                      <head>
                        <title>Atrium Storefront API</title>
                        <meta charset="utf-8" />
                        <meta name="viewport" content="width=device-width, initial-scale=1" />
                        <style>body { margin: 0; padding: 0; }</style>
                      </head>
                      <body>
                        <redoc spec-url="/openapi/v1.json"></redoc>
                        <script src="https://cdn.redoc.ly/redoc/v2.5.0/bundles/redoc.standalone.js"></script>
                      </body>
                    </html>
                    """,
                    "text/html"
                )
        )
        .AllowAnonymous()
        // The docs viewer is a UI convenience, not part of the API — keep it out of the OpenAPI document.
        .ExcludeFromDescription();
}

// The service boundary, stated once: everything this vertical serves lives under /storefront and needs
// an authenticated caller. Each feature maps its own relative subtree onto this group, so the routes
// nest the way the folders do (Storefront › Orders, Storefront › Reports).
var storefront = app.MapGroup("/storefront").RequireAuthorization();
storefront.MapOrderEndpoints();
storefront.MapReportEndpoints();

// The AG-UI support-agent endpoint at /storefront/agent (SSE), step-up-MFA gated (see SupportEndpoints).
storefront.MapSupportAgent();
storefront.MapSupportFeedback();

app.Run();
