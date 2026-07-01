namespace Atrium.Contracts;

public sealed record OrderItemRequest(int ProductId, int Quantity);

/// <summary>
/// Places an order for the given items. <see cref="IdempotencyKey"/> is a client-generated GUID for
/// one checkout attempt: the server dedupes on it, so a retry after an ambiguous failure (e.g. a
/// timeout after the order was already committed) returns the original order instead of creating a
/// duplicate. Reuse the same key when retrying the same checkout; use a fresh key for a new order.
/// </summary>
public sealed record CreateOrderRequest(IReadOnlyList<OrderItemRequest> Items, Guid IdempotencyKey);

public sealed record OrderLineDto(string ProductName, decimal UnitPrice, int Quantity);

public sealed record OrderDto(
    int Id,
    DateTime PlacedAtUtc,
    decimal Total,
    IReadOnlyList<OrderLineDto> Lines
);
