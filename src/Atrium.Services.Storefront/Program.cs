using Atrium.Services.Storefront;
using Atrium.Services.Storefront.Data;

var builder = WebApplication.CreateBuilder(args);

// This vertical's own database (no EF; Dapper over sprocs), plus the caller's HttpContext for token relay.
builder.AddSqlServerClient("storefrontdb");
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapOrderEndpoints();

app.Run();
