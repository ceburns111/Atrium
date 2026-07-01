namespace Atrium.Services.Storefront.Orders;

/// <summary>A flat row from <c>dbo.usp_Order_GetList</c> (one per order line); grouped into OrderDto.</summary>
public sealed record OrderRow(
    int OrderId,
    DateTime PlacedAtUtc,
    decimal Total,
    string ProductName,
    decimal UnitPrice,
    int Quantity
);
