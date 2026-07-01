-- Run-once (journaled): the catalog schema.
CREATE TABLE dbo.Categories (
    Id   INT IDENTITY(1, 1) CONSTRAINT PK_Categories PRIMARY KEY,
    Name NVARCHAR(64) NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE
);

CREATE TABLE dbo.Products (
    Id         INT IDENTITY(1, 1) CONSTRAINT PK_Products PRIMARY KEY,
    Name       NVARCHAR(128) NOT NULL,
    CategoryId INT NOT NULL CONSTRAINT FK_Products_Categories REFERENCES dbo.Categories (Id),
    Price      DECIMAL(10, 2) NOT NULL,
    Blurb      NVARCHAR(256) NOT NULL
);

CREATE INDEX IX_Products_CategoryId ON dbo.Products (CategoryId);
