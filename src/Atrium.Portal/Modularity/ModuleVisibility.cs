using System.Security.Claims;
using Atrium.Abstractions;

namespace Atrium.Portal.Modularity;

/// <summary>
/// The one role gate for module-contributed UI. A module (its nav entries and home card) is visible
/// when it declares no required role, or the signed-in user is in that role. Shared so the shell's
/// module-driven surfaces can't drift apart on what "visible" means.
/// </summary>
internal static class ModuleVisibility
{
    internal static bool IsVisible(IModule module, ClaimsPrincipal? user) =>
        module.RequiredRole is null || (user?.IsInRole(module.RequiredRole) ?? false);
}
