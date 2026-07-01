namespace Atrium.Abstractions;

/// <summary>A single navigation entry contributed by a module.</summary>
/// <param name="Title">Label shown in navigation.</param>
/// <param name="Path">Absolute route, e.g. "/storefront/cart".</param>
/// <param name="Icon">Optional icon key, resolved by the design system.</param>
/// <param name="RequiredRole">Optional role required to see this entry. Null = visible to all authenticated users.</param>
public sealed record NavItem(
    string Title,
    string Path,
    string? Icon = null,
    string? RequiredRole = null
);
