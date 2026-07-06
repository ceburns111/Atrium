-- Run-always: insert an order header and return its id, idempotently. If this checkout key was already
-- committed *by this user*, return the original order and IsNew = 0 so the caller skips re-inserting
-- line items. The unique index on IdempotencyKey is the integrity backstop against a concurrent
-- double-submit: the TRY/CATCH below turns the duplicate-key violation into either a replay (same
-- user won the race) or error 50002 (the key belongs to a different user — never leak their order).
-- Items are added by usp_OrderItem_Add, both inside one transaction owned by the repository.
CREATE OR ALTER PROCEDURE dbo.usp_Order_Create
    @UserName       NVARCHAR(128),
    @Total          DECIMAL(10, 2),
    @IdempotencyKey UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- User-scoped replay check: a key committed by someone else must NOT match here — that case
    -- falls through to the INSERT, where the unique index rejects it.
    DECLARE @ExistingId INT =
        (SELECT Id FROM dbo.Orders
         WHERE IdempotencyKey = @IdempotencyKey AND UserName = @UserName);

    IF @ExistingId IS NOT NULL
    BEGIN
        SELECT @ExistingId AS OrderId, CAST(0 AS BIT) AS IsNew;
        RETURN;
    END

    BEGIN TRY
        INSERT INTO dbo.Orders (UserName, PlacedAtUtc, Total, IdempotencyKey)
        VALUES (@UserName, SYSUTCDATETIME(), @Total, @IdempotencyKey);

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS OrderId, CAST(1 AS BIT) AS IsNew;
    END TRY
    BEGIN CATCH
        -- Only handle the unique-index violation (2601/2627); anything else is a real fault.
        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;

        -- Someone committed this key between our check and our insert. If it was this user
        -- (a concurrent double-submit), replay it; if it was another user, refuse.
        SET @ExistingId =
            (SELECT Id FROM dbo.Orders
             WHERE IdempotencyKey = @IdempotencyKey AND UserName = @UserName);

        IF @ExistingId IS NULL
            THROW 50002, 'Idempotency key is already in use.', 1;

        SELECT @ExistingId AS OrderId, CAST(0 AS BIT) AS IsNew;
    END CATCH
END
