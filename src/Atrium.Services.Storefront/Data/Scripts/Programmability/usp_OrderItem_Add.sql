-- Run-always: add one line to an existing order.
CREATE OR ALTER PROCEDURE dbo.usp_OrderItem_Add
    @OrderId     INT,
    @ProductName NVARCHAR(128),
    @UnitPrice   DECIMAL(10, 2),
    @Quantity    INT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.OrderItems (OrderId, ProductName, UnitPrice, Quantity)
    VALUES (@OrderId, @ProductName, @UnitPrice, @Quantity);
END
