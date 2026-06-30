namespace Atrium.Abstractions;

/// <summary>A single navigation entry contributed by a module.</summary>
/// <param name="Title">Label shown in navigation.</param>
/// <param name="Path">Absolute route, e.g. "/storefront/cart".</param>
/// <param name="Icon">Optional icon key, resolved by the design system.</param>
public sealed record NavItem(string Title, string Path, string? Icon = null);
