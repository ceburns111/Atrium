-- Run-always: insert an order header and return its new id. Items are added by usp_OrderItem_Add,
-- both inside one transaction owned by the repository.
CREATE OR ALTER PROCEDURE dbo.usp_Order_Create
    @UserName NVARCHAR(128),
    @Total    DECIMAL(10, 2)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Orders (UserName, PlacedAtUtc, Total)
    VALUES (@UserName, SYSUTCDATETIME(), @Total);

    SELECT CAST(SCOPE_IDENTITY() AS INT);
END
