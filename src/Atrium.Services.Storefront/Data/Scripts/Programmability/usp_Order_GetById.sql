-- Run-always: one order with its line items, scoped to its owner (flat rows; the repository groups).
-- The @UserName filter is the security boundary: a caller can only read an order they own.
CREATE OR ALTER PROCEDURE dbo.usp_Order_GetById
    @OrderId INT,
    @UserName NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   o.Id AS OrderId, o.PlacedAtUtc, o.Total, i.ProductName, i.UnitPrice, i.Quantity
    FROM     dbo.Orders o
    JOIN     dbo.OrderItems i ON i.OrderId = o.Id
    WHERE    o.Id = @OrderId AND o.UserName = @UserName
    ORDER BY i.ProductName;
END
