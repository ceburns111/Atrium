#:sdk Aspire.AppHost.Sdk@13.4.6
#:package Aspire.Hosting.SqlServer@13.4.6
#:project ../Atrium.Services.Catalog/Atrium.Services.Catalog.csproj
#:project ../Atrium.Gateway/Atrium.Gateway.csproj
#:project ../Atrium.Portal/Atrium.Portal.csproj
#:property UserSecretsId=atrium-apphost-secrets

using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// SQL Server (persisted in a Docker volume) with a database per service.
var sql = builder.AddSqlServer("sql").WithDataVolume();
var catalogDb = sql.AddDatabase("catalogdb");

// Catalog core service: owns product data, seeded + sprocs applied by DbUp on startup.
var catalog = builder
    .AddProject<Projects.Atrium_Services_Catalog>("catalog")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

// Gateway (YARP): the single ingress; routes /catalog/* to the catalog service by discovery.
var gateway = builder
    .AddProject<Projects.Atrium_Gateway>("gateway")
    .WithReference(catalog)
    .WaitFor(catalog);

// Portal (Blazor Server host): calls the gateway; module HttpClients resolve "https+http://gateway".
builder.AddProject<Projects.Atrium_Portal>("portal").WithReference(gateway).WaitFor(gateway);

builder.Build().Run();
