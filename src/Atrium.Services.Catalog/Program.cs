using Atrium.ServiceDefaults;
using Atrium.Services.Catalog.Catalog;
using Atrium.Services.Catalog.Data;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging + OpenTelemetry tracing (ASP.NET Core, HttpClient, SqlClient) exported
// over OTLP to the Aspire dashboard. SqlClient is on because this service owns a database.
builder.AddAtriumTelemetry(instrumentSqlClient: true);

// Aspire-injected "catalogdb" SqlConnection (scoped) — Dapper reads from it, no EF.
builder.AddSqlServerClient("catalogdb");
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddHealthChecks();

// Built-in OpenAPI (Microsoft.AspNetCore.OpenApi): the document is served at /openapi/v1.json and
// picks up each endpoint group's .WithTags(...) as OpenAPI tags. Registration is harmless in any
// environment; the document + docs page are only *exposed* in Development (see below).
builder.Services.AddOpenApi();

// Service discovery so the JWT-bearer JWKS backchannel can resolve "https+http://keycloak".
builder.Services.AddServiceDiscovery();
builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());

// Validate Keycloak-issued JWTs; require the shared "atrium" audience (stamped by the realm's mapper).
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

// Product writes are gated on the admin realm role; reads only need an authenticated caller.
builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy("admin", policy => policy.RequireRole("admin"));

var app = builder.Build();

// Apply schema + seed (run-once) and stored procedures (run-always) before serving traffic.
var connectionString =
    app.Configuration.GetConnectionString("catalogdb")
    ?? throw new InvalidOperationException("Connection string 'catalogdb' was not configured.");
DatabaseInitializer.Initialize(connectionString, app.Logger);

// One structured log event per request (method, path, status, elapsed); early so it wraps handlers.
app.UseAtriumRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// API docs, Development-only and anonymous: both routes are mapped at the app root (outside the
// bearer-only "/catalog" group) and AllowAnonymous, so the morning live-check can reach them without
// a token. /openapi/v1.json is the raw document; /docs renders it with Redoc (standalone, from CDN).
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
                        <title>Atrium Catalog API</title>
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

app.MapCatalogEndpoints();

app.Run();
