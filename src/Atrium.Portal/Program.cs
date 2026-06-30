using Atrium.Design;
using Atrium.Portal.Components;
using Atrium.Portal.Modularity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Design-system services available to every module.
builder.Services.AddScoped<ToastService>();

// Discover UI modules, let each register its own services, and expose the catalog to the shell + router.
// The host never names a module: a project reference is all it takes for one to light up.
var catalog = ModuleLoader.Discover();
foreach (var module in catalog.Modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}
builder.Services.AddSingleton(catalog);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Register module assemblies for server-side endpoint routing too, so deep-links / static SSR
    // resolve module pages — not just the interactive client-side router in Routes.razor.
    .AddAdditionalAssemblies([.. catalog.Assemblies]);

app.Run();
