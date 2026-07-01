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
        Guid idempotencyKey,
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
        Guid idempotencyKey,
        IReadOnlyList<OrderLineDto> lines,
        CancellationToken ct = default
    )
    {
        var total = lines.Sum(l => l.UnitPrice * l.Quantity);

        await db.OpenAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(ct);

        // Idempotent header insert: on a replay of an already-committed key the sproc returns the
        // original id with IsNew = 0, so we skip re-adding the lines (they're already there).
        var result = await db.QuerySingleAsync<OrderCreateResult>(
            new CommandDefinition(
                "dbo.usp_Order_Create",
                new
                {
                    UserName = userName,
                    Total = total,
                    IdempotencyKey = idempotencyKey,
                },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            )
        );

        if (result.IsNew)
        {
            foreach (var line in lines)
            {
                await db.ExecuteAsync(
                    new CommandDefinition(
                        "dbo.usp_OrderItem_Add",
                        new
                        {
                            OrderId = result.OrderId,
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
        }

        await transaction.CommitAsync(ct);

        if (result.IsNew)
        {
            logger.LogInformation(
                "Order {OrderId} created with {LineCount} line(s) totaling {OrderTotal}",
                result.OrderId,
                lines.Count,
                total
            );
        }
        else
        {
            logger.LogInformation(
                "Order {OrderId} idempotency-replayed for key {IdempotencyKey}; no duplicate created",
                result.OrderId,
                idempotencyKey
            );
        }

        return result.OrderId;
    }

    // The two-column projection of usp_Order_Create: the order id plus whether this call actually
    // inserted (true) or replayed an existing key (false).
    private sealed record OrderCreateResult(int OrderId, bool IsNew);

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
