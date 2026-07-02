#:sdk Aspire.AppHost.Sdk@13.4.6
#:package Aspire.Hosting.SqlServer@13.4.6
#:package Aspire.Hosting.Keycloak@13.4.6-preview.1.26319.6
#:project ../Atrium.Services.Catalog/Atrium.Services.Catalog.csproj
#:project ../Atrium.Services.Storefront/Atrium.Services.Storefront.csproj
#:project ../Atrium.Gateway/Atrium.Gateway.csproj
#:project ../Atrium.Portal/Atrium.Portal.csproj
#:property UserSecretsId=atrium-apphost-secrets

using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// SQL Server (persisted in a Docker volume) with a database per service.
var sql = builder.AddSqlServer("sql").WithDataVolume();
var catalogDb = sql.AddDatabase("catalogdb");
var storefrontDb = sql.AddDatabase("storefrontdb");

// Keycloak (identity core): imports the atrium realm on startup; fixed port so its URL is stable.
var keycloak = builder.AddKeycloak("keycloak", 8080).WithDataVolume().WithRealmImport("./realms");

// Catalog core service: owns product data (DbUp seed + sprocs) and validates Keycloak JWTs.
var catalog = builder
    .AddProject<Projects.Atrium_Services_Catalog>("catalog")
    .WithReference(catalogDb)
    .WithReference(keycloak)
    .WaitFor(catalogDb)
    .WaitFor(keycloak)
    .WithHttpHealthCheck("/health");

// Storefront app vertical: owns its own order database and calls the Catalog core to price orders.
var storefront = builder
    .AddProject<Projects.Atrium_Services_Storefront>("storefront")
    .WithReference(storefrontDb)
    .WithReference(catalog)
    .WithReference(keycloak)
    .WithEnvironment("SupportAgent__Provider", "Ollama")
    .WithEnvironment("SupportAgent__Endpoint", "http://localhost:11434/v1")
    .WithEnvironment("SupportAgent__Model", "qwen2.5:7b-instruct")
    .WithEnvironment("SupportAgent__GuardrailModel", "llama3.2:3b")
    .WaitFor(storefrontDb)
    .WaitFor(keycloak)
    .WithHttpHealthCheck("/health");

// Gateway (YARP): the single ingress; routes /catalog/* and /storefront/* to the services by discovery.
var gateway = builder
    .AddProject<Projects.Atrium_Gateway>("gateway")
    .WithReference(catalog)
    .WithReference(storefront)
    .WaitFor(catalog)
    .WaitFor(storefront);

// Portal (Blazor Server host): OIDC against Keycloak; calls the gateway with the user's bearer token.
builder
    .AddProject<Projects.Atrium_Portal>("portal")
    .WithReference(gateway)
    .WithReference(keycloak)
    .WithEnvironment("Keycloak__PortalSecret", "dev-portal-secret")
    .WaitFor(gateway)
    .WaitFor(keycloak);

builder.Build().Run();
