using Atrium.Abstractions;
using Atrium.Modules.Storefront;

namespace Atrium.UnitTests;

/// <summary>
/// The Storefront module contributes a "Support" chat surface through the <see cref="IModule"/>
/// seam, which the shell's assistant launcher renders. The endpoint must be gateway-relative with no
/// leading slash so <c>&lt;AgentChat&gt;</c> resolves it against the gateway base address (reaching the
/// AG-UI endpoint the service maps at <c>/storefront/agent</c>).
/// </summary>
public class StorefrontModuleTests
{
    [Fact]
    public void Declares_the_order_support_agent_surface()
    {
        var surface = Assert.Single(new StorefrontModule().AgentSurfaces);

        Assert.Equal("Support", surface.Name);
        Assert.Equal("storefront/agent", surface.Endpoint);
        Assert.DoesNotContain('/', surface.Endpoint[..1]); // no leading slash — resolves against the gateway base
        Assert.NotNull(surface.StarterPrompts);
        Assert.NotEmpty(surface.StarterPrompts!);
    }
}
