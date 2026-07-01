using System.Security.Claims;
using Atrium.Contracts;
using Atrium.Services.Storefront.Catalog;
using Atrium.Services.Storefront.Orders;
using Microsoft.AspNetCore.Http;

namespace Atrium.UnitTests.Support;

/// <summary>
/// Hand-written test doubles for the support-agent tools, so the tool logic is exercised without a
/// database, an HTTP call, or a mocking framework.
/// </summary>
internal static class SupportTestDoubles
{
    /// <summary>An <see cref="IHttpContextAccessor"/> carrying a signed-in user (preferred_username claim).</summary>
    public static IHttpContextAccessor HttpContextFor(string? userName)
    {
        var identity = userName is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim("preferred_username", userName)], "test");
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }
}

internal sealed class FakeOrderRepository(OrderDto? order = null) : IOrderRepository
{
    public string? LastUserName { get; private set; }

    public Task<OrderDto?> GetByIdAsync(
        int orderId,
        string userName,
        CancellationToken ct = default
    )
    {
        LastUserName = userName;
        return Task.FromResult(order?.Id == orderId ? order : null);
    }

    public Task<int> CreateAsync(
        string userName,
        Guid idempotencyKey,
        IReadOnlyList<OrderLineDto> lines,
        CancellationToken ct = default
    ) => throw new NotSupportedException();

    public Task<IReadOnlyList<OrderDto>> GetOrdersAsync(
        string userName,
        CancellationToken ct = default
    ) => throw new NotSupportedException();
}

internal sealed class FakeCatalogClient(params ProductDto[] products) : IStorefrontCatalogClient
{
    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProductDto>>(products);
}
