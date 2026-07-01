using Atrium.Abstractions;
using Atrium.Modules.Storefront.Cart;
using Atrium.Modules.Storefront.Catalog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atrium.Modules.Storefront;

public sealed class StorefrontModule : IModule
{
    public string Name => "Storefront";
    public string Description => "Browse the catalog, build a cart, and place an order.";
    public string BasePath => "/storefront";

    // Amber — Storefront's own identity, distinct from the shell's teal.
    public string? Accent => "#b45309";

    public IEnumerable<NavItem> NavItems => [new NavItem("Storefront", "/storefront")];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CartService>();

        // The catalog is reached through the gateway; service discovery resolves the logical name.
        services.AddHttpClient<CatalogClient>(client =>
            client.BaseAddress = new Uri("https+http://gateway")
        );
    }
}
