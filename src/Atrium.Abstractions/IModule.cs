using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atrium.Abstractions;

/// <summary>
/// A self-contained UI module hosted by the Atrium portal. Implement this in a Razor Class Library:
/// the host discovers it by reflection, lets it register its own services, surfaces it in navigation,
/// and routes to its routable components. Adding a module is a project reference plus this one type —
/// the host needs no compile-time knowledge of the module's internals.
/// </summary>
public interface IModule
{
    /// <summary>Display name shown on the portal homepage and in navigation.</summary>
    string Name { get; }

    /// <summary>One-line summary shown on the homepage card.</summary>
    string Description { get; }

    /// <summary>The route prefix the module owns, e.g. "/storefront".</summary>
    string BasePath { get; }

    /// <summary>Optional brand accent (hex) for the module's identity in the shell; falls back to the host accent.</summary>
    string? Accent => null;

    /// <summary>Navigation entries the module contributes to the shell.</summary>
    IEnumerable<NavItem> NavItems { get; }

    /// <summary>
    /// Registers the module's own services into the host container. Called once at startup before the
    /// app is built, so a module is wired exactly like first-party code.
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
