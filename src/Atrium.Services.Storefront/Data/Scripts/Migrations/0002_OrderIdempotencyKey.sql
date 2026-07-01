-- Run-once (journaled): give orders an idempotency key so a client retry after an ambiguous failure
-- (e.g. a timeout after the server already committed) returns the original order instead of creating a
-- duplicate. Nullable so rows created before this column existed stay valid; the filtered unique index
-- enforces one order per key while permitting many legacy NULLs. GO separates the batches so the index
-- can reference the freshly added column.
ALTER TABLE dbo.Orders ADD IdempotencyKey UNIQUEIDENTIFIER NULL;
GO
CREATE UNIQUE INDEX UX_Orders_IdempotencyKey
    ON dbo.Orders (IdempotencyKey)
    WHERE IdempotencyKey IS NOT NULL;
