namespace Atrium.Contracts;

public sealed record OrderItemRequest(int ProductId, int Quantity);

public sealed record CreateOrderRequest(IReadOnlyList<OrderItemRequest> Items);

public sealed record OrderLineDto(string ProductName, decimal UnitPrice, int Quantity);

public sealed record OrderDto(
    int Id,
    DateTime PlacedAtUtc,
    decimal Total,
    IReadOnlyList<OrderLineDto> Lines
);
