using Atrium.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atrium.Modules.Reports;

public sealed class ReportsModule : IModule
{
    public string Name => "Reports";

    public string Description =>
        "Sales analytics — revenue, orders, and a category breakdown drawn from real Storefront orders.";

    public string BasePath => "/reports";

    // Violet — Reports' own identity, distinct from the shell, Storefront (amber), and Admin (indigo).
    public string? Accent => "#7c3aed";

    public IEnumerable<NavItem> NavItems =>
        [new NavItem("Reports", "/reports", RequiredRole: "admin")];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Reports read the Storefront vertical's aggregate endpoint through the gateway.
        services.AddHttpClient<ReportsClient>(client =>
            client.BaseAddress = new Uri("https+http://gateway")
        );
    }
}
