-- Run-always: total number of orders placed (a headline report stat).
CREATE OR ALTER PROCEDURE dbo.usp_Report_OrderCount
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) FROM dbo.Orders;
END
