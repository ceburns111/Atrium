-- Run-once (journaled): the Storefront vertical's own order tables. Product data lives in the Catalog
-- core service; here we persist only what this slice owns.
CREATE TABLE dbo.Orders (
    Id          INT IDENTITY(1, 1) CONSTRAINT PK_Orders PRIMARY KEY,
    UserName    NVARCHAR(128) NOT NULL,
    PlacedAtUtc DATETIME2 NOT NULL,
    Total       DECIMAL(10, 2) NOT NULL
);

CREATE TABLE dbo.OrderItems (
    Id          INT IDENTITY(1, 1) CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId     INT NOT NULL CONSTRAINT FK_OrderItems_Orders REFERENCES dbo.Orders (Id),
    ProductName NVARCHAR(128) NOT NULL,
    UnitPrice   DECIMAL(10, 2) NOT NULL,
    Quantity    INT NOT NULL
);

CREATE INDEX IX_Orders_UserName ON dbo.Orders (UserName);
CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems (OrderId);
