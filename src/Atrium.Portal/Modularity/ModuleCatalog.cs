using System.Reflection;
using Atrium.Abstractions;

namespace Atrium.Portal.Modularity;

/// <summary>
/// The modules discovered at startup plus their assemblies. Registered as a singleton so the shell
/// (navigation, homepage) and the router (<c>AdditionalAssemblies</c>) draw from one source of truth.
/// </summary>
public sealed class ModuleCatalog(
    IReadOnlyList<IModule> modules,
    IReadOnlyList<Assembly> assemblies
)
{
    public IReadOnlyList<IModule> Modules { get; } = modules;
    public IReadOnlyList<Assembly> Assemblies { get; } = assemblies;
}
