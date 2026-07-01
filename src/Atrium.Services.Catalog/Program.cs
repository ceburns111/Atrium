using Atrium.Services.Catalog.Catalog;
using Atrium.Services.Catalog.Data;

var builder = WebApplication.CreateBuilder(args);

// Aspire-injected "catalogdb" SqlConnection (scoped) — Dapper reads from it, no EF.
builder.AddSqlServerClient("catalogdb");
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddHealthChecks();

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
DatabaseInitializer.Initialize(connectionString);

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapCatalogEndpoints();

app.Run();
