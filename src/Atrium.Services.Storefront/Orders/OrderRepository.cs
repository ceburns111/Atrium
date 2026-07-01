using System.Data;
using Atrium.Contracts;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

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

/// <summary>
/// Dapper-backed order store over stored procedures. Order creation inserts the header and each line
/// inside one transaction owned here; reads group the flat sproc rows into <see cref="OrderDto"/>.
/// </summary>
public sealed class OrderRepository(SqlConnection db, ILogger<OrderRepository> logger)
    : IOrderRepository
{
    public async Task<int> CreateAsync(
        string userName,
        IReadOnlyList<OrderLineDto> lines,
        CancellationToken ct = default
    )
    {
        var total = lines.Sum(l => l.UnitPrice * l.Quantity);

        await db.OpenAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(ct);

        var orderId = await db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "dbo.usp_Order_Create",
                new { UserName = userName, Total = total },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            )
        );

        foreach (var line in lines)
        {
            await db.ExecuteAsync(
                new CommandDefinition(
                    "dbo.usp_OrderItem_Add",
                    new
                    {
                        OrderId = orderId,
                        line.ProductName,
                        line.UnitPrice,
                        line.Quantity,
                    },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct
                )
            );
        }

        await transaction.CommitAsync(ct);
        logger.LogInformation(
            "Order {OrderId} created with {LineCount} line(s) totaling {OrderTotal}",
            orderId,
            lines.Count,
            total
        );
        return orderId;
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(
        string userName,
        CancellationToken ct = default
    )
    {
        var rows = await db.QueryAsync<OrderRow>(
            new CommandDefinition(
                "dbo.usp_Order_GetList",
                new { UserName = userName },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            )
        );

        return rows.GroupBy(r => new
            {
                r.OrderId,
                r.PlacedAtUtc,
                r.Total,
            })
            .Select(g => new OrderDto(
                g.Key.OrderId,
                g.Key.PlacedAtUtc,
                g.Key.Total,
                g.Select(r => new OrderLineDto(r.ProductName, r.UnitPrice, r.Quantity)).ToList()
            ))
            .ToList();
    }
}
