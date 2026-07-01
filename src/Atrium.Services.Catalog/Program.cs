using Atrium.Services.Catalog;
using Atrium.Services.Catalog.Data;

var builder = WebApplication.CreateBuilder(args);

// Aspire-injected "catalogdb" SqlConnection (scoped) — Dapper reads from it, no EF.
builder.AddSqlServerClient("catalogdb");
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Apply schema + seed (run-once) and stored procedures (run-always) before serving traffic.
var connectionString =
    app.Configuration.GetConnectionString("catalogdb")
    ?? throw new InvalidOperationException("Connection string 'catalogdb' was not configured.");
DatabaseInitializer.Initialize(connectionString);

app.MapHealthChecks("/health");
app.MapCatalogEndpoints();

app.Run();
