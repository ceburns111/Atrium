using Atrium.Abstractions;
using Atrium.Modules.Storefront.Cart;
using Atrium.Modules.Storefront.Catalog;
using Atrium.Modules.Storefront.Checkout;
using Atrium.Modules.Storefront.Orders;
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

    // The order-support chat surface the shell's assistant launcher renders. Endpoint is gateway-relative
    // (no leading slash) so <AgentChat> resolves it against the gateway base — reaching the AG-UI SSE
    // endpoint the Storefront service maps at /storefront/agent.
    public IEnumerable<AgentSurface> AgentSurfaces =>
        [
            new AgentSurface(
                "Order Support",
                "storefront/agent",
                StarterPrompts: ["Where's my order?", "Find me a desk lamp"]
            ),
        ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CartService>();

        // Persists/rehydrates the cart across the full-page sign-in round-trip (localStorage interop).
        services.AddScoped<CartPersistence>();

        // Mock payment authorizer for the checkout step — simulated only, never a real gateway.
        services.AddScoped<PaymentService>();

        // Both the catalog reads and the order writes go through the gateway; service discovery
        // resolves the logical name.
        services.AddHttpClient<CatalogClient>(client =>
            client.BaseAddress = new Uri("https+http://gateway")
        );
        services.AddHttpClient<OrdersClient>(client =>
            client.BaseAddress = new Uri("https+http://gateway")
        );
    }
}
