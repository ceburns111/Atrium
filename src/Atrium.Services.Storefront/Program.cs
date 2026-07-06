using Atrium.ServiceDefaults;
using Atrium.Services.Storefront.Catalog;
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
builder.Services.AddHttpClient<IStorefrontCatalogClient, StorefrontCatalogClient>(client =>
    client.BaseAddress = new Uri("https+http://catalog")
);

// Keycloak JWT validation (shared "atrium" realm/audience + claim mapping) and the "admin" policy —
// see AddAtriumJwtAuth. Most of this vertical only needs an authenticated caller (relayed bearer);
// Reports is the exception: the analytics surface is admin-only, matching the admin-gated Reports
// page/nav in the portal.
builder.AddAtriumJwtAuth();

var app = builder.Build();

var connectionString =
    app.Configuration.GetConnectionString("storefrontdb")
    ?? throw new InvalidOperationException("Connection string 'storefrontdb' was not configured.");
DatabaseInitializer.Initialize(connectionString, typeof(Program).Assembly, app.Logger);

// One structured log event per request (method, path, status, elapsed); early so it wraps handlers.
app.UseAtriumRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// API docs, Development-only and anonymous: both routes are mapped at the app root (outside the
// bearer-only "/storefront" group) so the morning live-check can reach them without a token.
// /openapi/v1.json is the raw document; /docs renders it with Redoc — see MapAtriumApiDocs.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapAtriumApiDocs("Atrium Storefront API");
}

// The service boundary, stated once: everything this vertical serves lives under /storefront and needs
// an authenticated caller. Each feature maps its own relative subtree onto this group, so the routes
// nest the way the folders do (Storefront › Orders, Storefront › Reports).
var storefront = app.MapGroup("/storefront").RequireAuthorization();
storefront.MapOrderEndpoints();
storefront.MapReportEndpoints();

app.Run();
