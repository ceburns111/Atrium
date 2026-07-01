using Atrium.ServiceDefaults;
using Atrium.Services.Storefront.Catalog;
using Atrium.Services.Storefront.Data;
using Atrium.Services.Storefront.Orders;
using Atrium.Services.Storefront.Reports;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging + OpenTelemetry tracing (ASP.NET Core, HttpClient, SqlClient) exported
// over OTLP to the Aspire dashboard. SqlClient is on because this vertical owns a database.
builder.AddAtriumTelemetry(instrumentSqlClient: true);

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
builder.Services.AddHttpClient<StorefrontCatalogClient>(client =>
    client.BaseAddress = new Uri("https+http://catalog")
);

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
            options.TokenValidationParameters.NameClaimType = "preferred_username";
        }
    );
builder.Services.AddAuthorization();

var app = builder.Build();

var connectionString =
    app.Configuration.GetConnectionString("storefrontdb")
    ?? throw new InvalidOperationException("Connection string 'storefrontdb' was not configured.");
DatabaseInitializer.Initialize(connectionString);

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
                        <script src="https://cdn.redoc.ly/redoc/latest/bundles/redoc.standalone.js"></script>
                      </body>
                    </html>
                    """,
                    "text/html"
                )
        )
        .AllowAnonymous();
}

// The service boundary, stated once: everything this vertical serves lives under /storefront and needs
// an authenticated caller. Each feature maps its own relative subtree onto this group, so the routes
// nest the way the folders do (Storefront › Orders, Storefront › Reports).
var storefront = app.MapGroup("/storefront").RequireAuthorization();
storefront.MapOrderEndpoints();
storefront.MapReportEndpoints();

app.Run();
