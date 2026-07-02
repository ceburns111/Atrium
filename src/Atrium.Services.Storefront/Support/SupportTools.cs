using System.ComponentModel;
using System.Security.Claims;
using Atrium.Services.Storefront.Catalog;
using Atrium.Services.Storefront.Orders;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// The functions the support agent may call. Each method carries a <see cref="DescriptionAttribute"/>
/// the model reads to decide when to invoke it (via <c>AIFunctionFactory.Create</c>). The tools run in
/// the caller's request scope, so they read the signed-in user from <see cref="IHttpContextAccessor"/>
/// and answer only from that user's real data — never inventing state the store doesn't track.
/// </summary>
public sealed class SupportTools(
    IHttpContextAccessor httpContext,
    IOrderRepository orders,
    IStorefrontCatalogClient catalog
)
{
    [Description("Look up the status of one of the signed-in customer's orders by its id.")]
    public async Task<string> GetOrderStatus(int orderId)
    {
        var userName = ResolveUserName();
        if (string.IsNullOrEmpty(userName))
        {
            return "I can't look that up — you don't appear to be signed in.";
        }

        var order = await orders.GetByIdAsync(orderId, userName);
        if (order is null)
        {
            return $"I couldn't find an order #{orderId} on your account.";
        }

        // There is no status column in the store, so we report only what the data honestly supports:
        // a placed-and-confirmed order. We never fabricate Shipped/Delivered milestones.
        var itemCount = order.Lines.Sum(l => l.Quantity);
        return $"Order #{order.Id} — Confirmed. Placed {order.PlacedAtUtc:d}, "
            + $"{itemCount} item(s), total {order.Total:C}.";
    }

    [Description("Find products in the catalog by name.")]
    public async Task<string> FindProduct(string query)
    {
        // A blank query would otherwise match every product and return an arbitrary first five; ask the
        // caller to narrow it instead of guessing.
        if (string.IsNullOrWhiteSpace(query))
        {
            return "What product are you looking for? Tell me a name or keyword and I'll search the catalog.";
        }

        var products = await catalog.GetProductsAsync();
        var matches = products
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (matches.Count == 0)
        {
            return $"I couldn't find any products matching \"{query}\".";
        }

        var lines = matches.Select(p => $"- {p.Name} ({p.Price:C})");
        return $"Here's what I found for \"{query}\":\n" + string.Join("\n", lines);
    }

    // The JWT sets NameClaimType = preferred_username, so both the explicit claim and Identity.Name
    // resolve to the Keycloak username the order store is keyed on.
    private string? ResolveUserName()
    {
        var user = httpContext.HttpContext?.User;
        return user?.FindFirstValue("preferred_username") ?? user?.Identity?.Name;
    }
}
