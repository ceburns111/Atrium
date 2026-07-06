using Atrium.ServiceDefaults;
using Atrium.Services.Catalog.Catalog;

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

// Keycloak JWT validation (shared "atrium" realm/audience + claim mapping) and the "admin" policy —
// see AddAtriumJwtAuth. Product writes are gated on the admin realm role; reads only need an
// authenticated caller.
builder.AddAtriumJwtAuth();

var app = builder.Build();

// Apply schema + seed (run-once) and stored procedures (run-always) before serving traffic.
var connectionString =
    app.Configuration.GetConnectionString("catalogdb")
    ?? throw new InvalidOperationException("Connection string 'catalogdb' was not configured.");
DatabaseInitializer.Initialize(connectionString, typeof(Program).Assembly, app.Logger);

// One structured log event per request (method, path, status, elapsed); early so it wraps handlers.
app.UseAtriumRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// API docs, Development-only and anonymous: both routes are mapped at the app root (outside the
// bearer-only "/catalog" group) so the morning live-check can reach them without a token.
// /openapi/v1.json is the raw document; /docs renders it with Redoc — see MapAtriumApiDocs.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapAtriumApiDocs("Atrium Catalog API");
}

app.MapCatalogEndpoints();

app.Run();
