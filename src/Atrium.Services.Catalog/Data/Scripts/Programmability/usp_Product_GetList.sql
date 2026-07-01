-- Run-always (CREATE OR ALTER, not journaled): list products, optionally filtered by category name.
CREATE OR ALTER PROCEDURE dbo.usp_Product_GetList
    @CategoryName NVARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.Id, p.Name, c.Name AS CategoryName, p.Price, p.Blurb
    FROM   dbo.Products p
    JOIN   dbo.Categories c ON c.Id = p.CategoryId
    WHERE  (@CategoryName IS NULL OR c.Name = @CategoryName)
    ORDER  BY p.Name;
END
