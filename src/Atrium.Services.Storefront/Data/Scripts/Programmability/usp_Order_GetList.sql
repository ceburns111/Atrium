-- Run-always: a user's orders with their line items, newest first (flat rows; the repository groups).
CREATE OR ALTER PROCEDURE dbo.usp_Order_GetList
    @UserName NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   o.Id AS OrderId, o.PlacedAtUtc, o.Total, i.ProductName, i.UnitPrice, i.Quantity
    FROM     dbo.Orders o
    JOIN     dbo.OrderItems i ON i.OrderId = o.Id
    WHERE    o.UserName = @UserName
    ORDER BY o.PlacedAtUtc DESC, o.Id DESC;
END
