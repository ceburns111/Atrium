using System.Reflection;
using Atrium.Abstractions;

namespace Atrium.Portal.Modularity;

/// <summary>
/// Discovers UI modules by convention: any "Atrium.Modules.*" assembly in the app directory that
/// exposes an <see cref="IModule"/>. Referenced RCLs land in the app directory at build, so a single
/// project reference is all it takes to be discovered.
/// </summary>
/// <remarks>
/// Assemblies are resolved by name into the default load context (they're real project references, so
/// they're in the app's dependency manifest). A future runtime folder-drop would swap this one step
/// for an <c>AssemblyLoadContext</c> over a /modules directory — the modules themselves stay unchanged.
/// </remarks>
public static class ModuleLoader
{
    public static ModuleCatalog Discover()
    {
        var modules = new List<IModule>();
        var assemblies = new List<Assembly>();

        foreach (
            var dll in Directory.EnumerateFiles(AppContext.BaseDirectory, "Atrium.Modules.*.dll")
        )
        {
            var name = AssemblyName.GetAssemblyName(dll);
            var assembly =
                AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().FullName == name.FullName)
                ?? Assembly.Load(name);

            var found = assembly
                .GetTypes()
                .Where(t =>
                    t is { IsClass: true, IsAbstract: false } && typeof(IModule).IsAssignableFrom(t)
                )
                .Select(t => (IModule)Activator.CreateInstance(t)!)
                .ToList();

            if (found.Count > 0)
            {
                modules.AddRange(found);
                assemblies.Add(assembly);
            }
        }

        return new ModuleCatalog(modules, assemblies);
    }
}
