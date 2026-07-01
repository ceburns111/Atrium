using Atrium.Contracts;
using Atrium.Services.Storefront.Orders;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
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
        StorefrontDb.Initialize(_connectionString, NullLogger.Instance);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private OrderRepository NewRepository() =>
        new(new SqlConnection(_connectionString), NullLogger<OrderRepository>.Instance);

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

        var orderId = await NewRepository().CreateAsync(user, Guid.NewGuid(), lines);

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

        await NewRepository().CreateAsync(user, Guid.NewGuid(), lines);

        // The list sproc joins Orders×OrderItems (three flat rows); grouping must collapse them to one order.
        var order = Assert.Single(await NewRepository().GetOrdersAsync(user));
        Assert.Equal(3, order.Lines.Count);
        Assert.Contains(order.Lines, l => l.ProductName == "Task Lamp" && l.Quantity == 1);
    }

    [Fact]
    public async Task Create_is_idempotent_for_a_repeated_key()
    {
        // Simulates a retry after an ambiguous failure: the same checkout key submitted twice must
        // yield the original order — no second header, no duplicated lines.
        const string user = "alice-idempotent";
        var key = Guid.NewGuid();
        var lines = new[] { new OrderLineDto("Task Lamp", 79m, 1) };

        var first = await NewRepository().CreateAsync(user, key, lines);
        var second = await NewRepository().CreateAsync(user, key, lines);

        Assert.Equal(first, second); // same id, not a fresh order

        var order = Assert.Single(await NewRepository().GetOrdersAsync(user));
        Assert.Equal(first, order.Id);
        Assert.Single(order.Lines); // the replay did not re-add the line
    }

    [Fact]
    public async Task GetById_returns_the_owner_s_order_with_its_total_and_lines()
    {
        const string user = "bob-getbyid";
        var lines = new[]
        {
            new OrderLineDto("Monitor Arm", 120m, 1), // 120
            new OrderLineDto("Keyboard", 90m, 2), // 180
        };

        var orderId = await NewRepository().CreateAsync(user, Guid.NewGuid(), lines);

        var order = await NewRepository().GetByIdAsync(orderId, user);

        Assert.NotNull(order);
        Assert.Equal(orderId, order.Id);
        Assert.Equal(300m, order.Total); // 120 + 180
        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public async Task GetById_returns_null_for_an_unknown_order()
    {
        const string user = "bob-getbyid-missing";

        // A never-created id under this user must not resolve to anything.
        var order = await NewRepository().GetByIdAsync(2_000_000_000, user);

        Assert.Null(order);
    }

    [Fact]
    public async Task GetById_returns_null_when_the_order_belongs_to_another_user()
    {
        // Security boundary: an order created by X must be invisible to Y even with the right id.
        const string owner = "carol-owner";
        const string other = "dave-intruder";
        var lines = new[] { new OrderLineDto("Task Lamp", 79m, 1) };

        var orderId = await NewRepository().CreateAsync(owner, Guid.NewGuid(), lines);

        Assert.Null(await NewRepository().GetByIdAsync(orderId, other));
        Assert.NotNull(await NewRepository().GetByIdAsync(orderId, owner)); // still readable by its owner
    }
}
