-- Run-always (CREATE OR ALTER): insert a product and return the created row (joined to its category).
CREATE OR ALTER PROCEDURE dbo.usp_Product_Create
    @Name         NVARCHAR(128),
    @CategoryName NVARCHAR(64),
    @Price        DECIMAL(10, 2),
    @Blurb        NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CategoryId INT = (SELECT Id FROM dbo.Categories WHERE Name = @CategoryName);
    IF @CategoryId IS NULL
        THROW 50001, 'Unknown category.', 1;

    INSERT INTO dbo.Products (Name, CategoryId, Price, Blurb)
    VALUES (@Name, @CategoryId, @Price, @Blurb);

    DECLARE @Id INT = CAST(SCOPE_IDENTITY() AS INT);

    SELECT p.Id, p.Name, c.Name AS CategoryName, p.Price, p.Blurb
    FROM   dbo.Products p
    JOIN   dbo.Categories c ON c.Id = p.CategoryId
    WHERE  p.Id = @Id;
END
