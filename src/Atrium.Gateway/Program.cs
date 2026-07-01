var builder = WebApplication.CreateBuilder(args);

// The edge gateway: a pure YARP reverse proxy. Routes come from configuration; destinations are logical
// Aspire service names ("https+http://catalog") resolved at runtime by service discovery. The Portal
// attaches the bearer token itself, so the gateway just forwards requests (Authorization header included).
builder.Services.AddServiceDiscovery();
builder
    .Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.MapReverseProxy();

app.Run();
