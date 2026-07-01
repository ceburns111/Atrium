using Atrium.Contracts;

namespace Atrium.Services.Storefront.Orders;

public interface IOrderRepository
{
    Task<int> CreateAsync(
        string userName,
        IReadOnlyList<OrderLineDto> lines,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(string userName, CancellationToken ct = default);
}
