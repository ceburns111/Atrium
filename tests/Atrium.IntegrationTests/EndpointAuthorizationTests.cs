using System.Net;
using System.Net.Http.Json;
using System.Text;
using Atrium.Contracts;
using Atrium.Services.Catalog.Catalog;
using Atrium.Services.Storefront.Orders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Atrium.IntegrationTests;

/// <summary>
/// I3 — the anonymous-browse / gated-checkout security posture, asserted at the real HTTP boundary by
/// booting each service in-process (WebApplicationFactory) against the shared SQL container. No token is
/// ever sent: an anonymous catalog read must succeed (the storefront is browsable signed-out), while an
/// anonymous order placement must be rejected before the handler (checkout stays server-gated). This is
/// the security half of item B — the endpoints' authorization metadata is what these tests trust.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class EndpointAuthorizationTests(SqlServerFixture sql)
{
    /// <summary>
    /// Boots one real service host. Keyed off a non-static public type in the target assembly (a static
    /// endpoints class can't be a generic type argument); WebApplicationFactory resolves that assembly's
    /// generated entry point (Program). The Aspire-injected connection string is overridden to point at
    /// the throwaway SQL container so the startup DbUp init and any read hit a real database.
    /// </summary>
    private sealed class ServiceFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
        where TEntryPoint : class
    {
        // Aspire's AddSqlServerClient binds the connection string eagerly while Program runs — before
        // WebApplicationFactory's ConfigureWebHost hooks apply — so seed it as an environment variable,
        // which the builder's default configuration reads at CreateBuilder time.
        public ServiceFactory(string connectionName, string connectionString) =>
            Environment.SetEnvironmentVariable(
                $"ConnectionStrings__{connectionName}",
                connectionString
            );

        // Development keeps JWT metadata resolvable over plain HTTP; anonymous requests never validate a
        // token, so Keycloak is never actually contacted from these tests.
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseEnvironment("Development");
    }

    [Fact]
    public async Task Anonymous_catalog_read_is_allowed()
    {
        using var factory = new ServiceFactory<CatalogRepository>(
            "catalogdb",
            sql.ConnectionStringFor("catalog_authz")
        );
        using var client = factory.CreateClient();

        // No Authorization header: browsing the catalog signed-out must still succeed.
        using var response = await client.GetAsync("/catalog/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<IReadOnlyList<ProductDto>>();
        Assert.NotNull(products);
    }

    [Fact]
    public async Task Anonymous_order_placement_is_rejected()
    {
        using var factory = new ServiceFactory<OrderRepository>(
            "storefrontdb",
            sql.ConnectionStringFor("storefront_authz")
        );
        using var client = factory.CreateClient();

        var request = new CreateOrderRequest([new OrderItemRequest(1, 1)], Guid.NewGuid());

        // No Authorization header: the /storefront group's RequireAuthorization must reject checkout
        // before the handler runs — the server-side gate holds regardless of the anonymous UI.
        using var response = await client.PostAsJsonAsync("/storefront/orders", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_support_agent_request_is_rejected()
    {
        using var factory = new ServiceFactory<OrderRepository>(
            "storefrontdb",
            sql.ConnectionStringFor("storefront_agent_authz")
        );
        using var client = factory.CreateClient();

        // The AG-UI support endpoint is mapped and carries the StepUpMfa policy, so an anonymous POST
        // (no Authorization header) must be challenged with 401 before the agent runs — it is never
        // anonymous. A POST is required because the route is POST-only; the body is irrelevant since
        // authorization short-circuits before model binding. (The policy's step-up logic itself is
        // covered deterministically by StepUpMfaHandlerTests.)
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/storefront/agent", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
