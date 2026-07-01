using Atrium.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atrium.Modules.Admin;

public sealed class AdminModule : IModule
{
    public string Name => "Admin";

    public string Description =>
        "Back-office catalog management — create and edit products. Saving requires the admin role.";

    public string BasePath => "/admin";

    // Indigo — the back-office's own identity, distinct from the shell's teal and Storefront's amber.
    public string? Accent => "#4f46e5";

    public IEnumerable<NavItem> NavItems => [new NavItem("Admin", "/admin")];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Admin reads and writes go to the Catalog core through the gateway; the writes are enforced
        // server-side against the admin role, so the module just calls them with the user's token.
        services.AddHttpClient<AdminCatalogClient>(client =>
            client.BaseAddress = new Uri("https+http://gateway")
        );
    }
}
