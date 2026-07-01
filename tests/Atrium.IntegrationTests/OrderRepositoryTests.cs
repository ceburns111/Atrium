using Atrium.Contracts;
using Atrium.Services.Storefront.Orders;
using Microsoft.Data.SqlClient;
using StorefrontDb = Atrium.Services.Storefront.Data.DatabaseInitializer;

namespace Atrium.IntegrationTests;

/// <summary>
/// I2 — order creation and read-back against a real SQL Server. Concept on show: a multi-statement
/// transaction owned by the repository (the header sproc plus one line sproc per item, committed
/// together), and the read that reverses it — the list sproc returns flat header×line rows which the
/// repository regroups into a single <see cref="OrderDto"/> with its lines.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class OrderRepositoryTests : IAsyncLifetime
{
    private readonly string _connectionString;

    public OrderRepositoryTests(SqlServerFixture sql) =>
        _connectionString = sql.ConnectionStringFor("storefront_test");

    public ValueTask InitializeAsync()
    {
        StorefrontDb.Initialize(_connectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private OrderRepository NewRepository() => new(new SqlConnection(_connectionString));

    [Fact]
    public async Task Create_persists_the_header_and_all_lines_in_one_transaction()
    {
        // A distinct user isolates this test's rows from any other test on the shared database.
        const string user = "alice-create";
        var lines = new[]
        {
            new OrderLineDto("Desk Mat", 39m, 2), // 78
            new OrderLineDto("Headphones", 199m, 1), // 199
        };

        var orderId = await NewRepository().CreateAsync(user, lines);

        Assert.True(orderId > 0);

        var orders = await NewRepository().GetOrdersAsync(user);
        var order = Assert.Single(orders);
        Assert.Equal(orderId, order.Id);
        Assert.Equal(277m, order.Total); // 78 + 199, computed by the repository at write time
        Assert.Equal(2, order.Lines.Count); // both lines came back
    }

    [Fact]
    public async Task GetOrders_regroups_flat_rows_into_one_order_per_header()
    {
        const string user = "alice-grouping";
        var lines = new[]
        {
            new OrderLineDto("Cork Coaster Set", 18m, 4),
            new OrderLineDto("Task Lamp", 79m, 1),
            new OrderLineDto("Felt Cable Tray", 29m, 2),
        };

        await NewRepository().CreateAsync(user, lines);

        // The list sproc joins Orders×OrderItems (three flat rows); grouping must collapse them to one order.
        var order = Assert.Single(await NewRepository().GetOrdersAsync(user));
        Assert.Equal(3, order.Lines.Count);
        Assert.Contains(order.Lines, l => l.ProductName == "Task Lamp" && l.Quantity == 1);
    }
}
