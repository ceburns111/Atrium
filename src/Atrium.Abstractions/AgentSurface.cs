namespace Atrium.Abstractions;

/// <summary>A chat surface a module contributes, rendered by the shell's assistant launcher.</summary>
/// <param name="Name">Label shown in the assistant launcher, e.g. "Order Support".</param>
/// <param name="Endpoint">
/// Gateway-relative path to the module's agent, with <b>no</b> leading slash, e.g. "storefront/agent" —
/// &lt;AgentChat&gt; resolves it against the gateway base address. (This is a service-topology path, unlike
/// <see cref="NavItem.Path"/>, which is an absolute portal route.)
/// </param>
/// <param name="StarterPrompts">Optional suggested prompts to seed the conversation. Null = none.</param>
/// <param name="Icon">Optional icon key, resolved by the design system.</param>
public sealed record AgentSurface(
    string Name,
    string Endpoint,
    string[]? StarterPrompts = null,
    string? Icon = null
);
