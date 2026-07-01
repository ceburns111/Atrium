namespace Atrium.Contracts;

/// <summary>Admin payload to create a catalog product. Category is by name; the service resolves the id.</summary>
public sealed record CreateProductRequest(
    string Name,
    string Category,
    decimal Price,
    string Blurb
);

/// <summary>Admin payload to update an existing catalog product (id comes from the route).</summary>
public sealed record UpdateProductRequest(
    string Name,
    string Category,
    decimal Price,
    string Blurb
);
