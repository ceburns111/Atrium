-- Run-always (CREATE OR ALTER, not journaled): categories with their product counts.
CREATE OR ALTER PROCEDURE dbo.usp_Category_GetList
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   c.Name, COUNT(p.Id) AS ProductCount
    FROM     dbo.Categories c
    LEFT JOIN dbo.Products p ON p.CategoryId = c.Id
    GROUP BY c.Name
    ORDER BY c.Name;
END
