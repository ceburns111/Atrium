-- Run-always: insert an order header and return its id, idempotently. If this checkout key was already
-- committed, return the original order and IsNew = 0 so the caller skips re-inserting line items; the
-- unique index on IdempotencyKey is the integrity backstop against a concurrent double-submit. Items
-- are added by usp_OrderItem_Add, both inside one transaction owned by the repository.
CREATE OR ALTER PROCEDURE dbo.usp_Order_Create
    @UserName       NVARCHAR(128),
    @Total          DECIMAL(10, 2),
    @IdempotencyKey UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ExistingId INT =
        (SELECT Id FROM dbo.Orders WHERE IdempotencyKey = @IdempotencyKey);

    IF @ExistingId IS NOT NULL
    BEGIN
        SELECT @ExistingId AS OrderId, CAST(0 AS BIT) AS IsNew;
        RETURN;
    END

    INSERT INTO dbo.Orders (UserName, PlacedAtUtc, Total, IdempotencyKey)
    VALUES (@UserName, SYSUTCDATETIME(), @Total, @IdempotencyKey);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS OrderId, CAST(1 AS BIT) AS IsNew;
END
