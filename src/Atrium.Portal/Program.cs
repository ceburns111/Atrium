using System.Security.Claims;
using Atrium.Design;
using Atrium.Portal.Components;
using Atrium.Portal.Modularity;
using Atrium.ServiceDefaults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging + OpenTelemetry tracing (Blazor Server ASP.NET Core + the module
// HttpClients to the gateway), so a trace starts here and follows Portal → Gateway → Service → SQL.
builder.AddAtriumTelemetry();

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Design-system services available to every module.
builder.Services.AddScoped<ToastService>();

// AG-UI chat plumbing for the <AgentChat> primitive (named gateway client + circuit-scoped factory that
// attaches the signed-in user's bearer). Registered here so it shares the AccessTokenHolder + service
// discovery this host already sets up for module clients.
builder.Services.AddAgentChat();

// Every module HttpClient gets service discovery (resolve "https+http://gateway") and the signed-in
// user's bearer token, applied by default so modules don't wire it themselves.
builder.Services.AddServiceDiscovery();
builder.Services.AddScoped<AccessTokenHolder>();
builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());

// --- Authentication: cookie session + OIDC code flow to Keycloak. In Blazor Server the server holds
// the tokens directly, so the separate BFF of the WASM design folds into the host. ---
var keycloakRealm = builder.Configuration["Keycloak:Realm"] ?? "atrium";
var keycloakAuthority =
    (
        builder.Configuration["services:keycloak:https:0"]
        ?? builder.Configuration["services:keycloak:http:0"]
        ?? throw new InvalidOperationException(
            "Keycloak endpoint not found — did apphost WithReference(keycloak) the portal?"
        )
    ) + $"/realms/{keycloakRealm}";

builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        // A wrong-role user reaching a role-gated route by full-page GET (deep-link / refresh /
        // bookmark) is denied at the endpoint (403); send them to the clean Forbidden page instead
        // of the default /Account/AccessDenied, which isn't a route and falls through to "Not Found".
        options.AccessDeniedPath = "/forbidden";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = keycloakAuthority;
        options.ClientId = "atrium-portal";
        options.ClientSecret = builder.Configuration["Keycloak:PortalSecret"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.RoleClaimType = "role";

        // Stash the access token as a claim so the Blazor circuit (which has no HttpContext) can attach
        // it to outbound API calls. Dev-simple; production would use a token store with refresh
        // (e.g. Duende.AccessTokenManagement) rather than parking the token in the principal.
        options.Events.OnTokenValidated = context =>
        {
            var accessToken = context.TokenEndpointResponse?.AccessToken;
            if (
                !string.IsNullOrEmpty(accessToken)
                && context.Principal?.Identity is ClaimsIdentity identity
            )
            {
                identity.AddClaim(new Claim("access_token", accessToken));
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Discover UI modules, let each register its own services, and expose the catalog to the shell + router.
var moduleCatalog = ModuleLoader.Discover();
foreach (var module in moduleCatalog.Modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}
builder.Services.AddSingleton(moduleCatalog);

var app = builder.Build();

// One structured log event per request (method, path, status, elapsed); early so it wraps handlers.
app.UseAtriumRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Login/logout endpoints — components trigger these with a full-page navigation.
app.MapGet(
    "/account/login",
    (string? returnUrl) =>
    {
        var target =
            returnUrl is not null
            && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            && returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//")
                ? returnUrl
                : "/";
        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = target },
            [OpenIdConnectDefaults.AuthenticationScheme]
        );
    }
);

// Deliberately a GET, not the POST+antiforgery convention for sign-out: a demo-scale trade-off
// (a cross-site GET or link prefetch could sign the user out — annoying, never dangerous).
app.MapGet(
    "/account/logout",
    () =>
        Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme,
            ]
        )
);

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Register module assemblies for server-side endpoint routing too, so deep-links / static SSR
    // resolve module pages — not just the interactive client-side router in Routes.razor.
    .AddAdditionalAssemblies([.. moduleCatalog.Assemblies]);

app.Run();
