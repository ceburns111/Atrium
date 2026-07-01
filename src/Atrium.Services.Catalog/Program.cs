using Atrium.Services.Catalog;
using Atrium.Services.Catalog.Data;

var builder = WebApplication.CreateBuilder(args);

// Aspire-injected "catalogdb" SqlConnection (scoped) — Dapper reads from it, no EF.
builder.AddSqlServerClient("catalogdb");
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddHealthChecks();

// Service discovery so the JWT-bearer JWKS backchannel can resolve "https+http://keycloak".
builder.Services.AddServiceDiscovery();
builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());

// Validate Keycloak-issued JWTs; require the atrium-catalog audience (stamped by the realm's mapper).
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

// Apply schema + seed (run-once) and stored procedures (run-always) before serving traffic.
var connectionString =
    app.Configuration.GetConnectionString("catalogdb")
    ?? throw new InvalidOperationException("Connection string 'catalogdb' was not configured.");
DatabaseInitializer.Initialize(connectionString);

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapCatalogEndpoints();

app.Run();
