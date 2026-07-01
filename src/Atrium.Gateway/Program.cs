using Atrium.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging + OpenTelemetry tracing (ASP.NET Core + the proxied HttpClient) so the
// gateway hop shows up in the same distributed trace. No SqlClient — the gateway owns no database.
builder.AddAtriumTelemetry();

// The edge gateway: a pure YARP reverse proxy. Routes come from configuration; destinations are logical
// Aspire service names ("https+http://catalog") resolved at runtime by service discovery. The Portal
// attaches the bearer token itself, so the gateway just forwards requests (Authorization header included).
builder.Services.AddServiceDiscovery();
builder
    .Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

// One structured log event per proxied request; early so it wraps the reverse-proxy pipeline.
app.UseAtriumRequestLogging();

app.MapReverseProxy();

app.Run();
