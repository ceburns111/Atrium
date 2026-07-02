namespace Atrium.Abstractions;

/// <summary>A chat surface a module contributes, rendered by the shell's assistant launcher.</summary>
/// <param name="Name">Label shown in the assistant launcher, e.g. "Order Support".</param>
/// <param name="Endpoint">Gateway-relative path to the module's agent, e.g. "/storefront/agent".</param>
/// <param name="StarterPrompts">Optional suggested prompts to seed the conversation. Null = none.</param>
/// <param name="Icon">Optional icon key, resolved by the design system.</param>
public sealed record AgentSurface(
    string Name,
    string Endpoint,
    string[]? StarterPrompts = null,
    string? Icon = null
);
