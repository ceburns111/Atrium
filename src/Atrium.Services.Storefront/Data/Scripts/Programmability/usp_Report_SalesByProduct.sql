-- Run-always: units and revenue per product across all orders. The caller re-buckets these into
-- categories using the Catalog product->category map (order rows store only the product name).
CREATE OR ALTER PROCEDURE dbo.usp_Report_SalesByProduct
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   ProductName,
             SUM(Quantity)             AS Units,
             SUM(UnitPrice * Quantity) AS Revenue
    FROM     dbo.OrderItems
    GROUP BY ProductName;
END
