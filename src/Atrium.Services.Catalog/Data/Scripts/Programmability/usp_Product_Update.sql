-- Run-always (CREATE OR ALTER): update a product and return the updated row (empty if the id is unknown).
CREATE OR ALTER PROCEDURE dbo.usp_Product_Update
    @Id           INT,
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

    UPDATE dbo.Products
    SET    Name = @Name, CategoryId = @CategoryId, Price = @Price, Blurb = @Blurb
    WHERE  Id = @Id;

    SELECT p.Id, p.Name, c.Name AS CategoryName, p.Price, p.Blurb
    FROM   dbo.Products p
    JOIN   dbo.Categories c ON c.Id = p.CategoryId
    WHERE  p.Id = @Id;
END
