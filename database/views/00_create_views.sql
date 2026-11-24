/*
    CvErp — Bộ view báo cáo quản trị
    Chạy: sqlcmd -S .\SQLEXPRESS -E -C -i database\views\00_create_views.sql
    Yêu cầu: database CvErp đã được tạo bằng EF migration (dotnet ef database update).
*/

USE CvErp;
GO

/* ============================================================
   1. Tồn kho hiện tại theo vật tư và kho
   ============================================================ */
CREATE OR ALTER VIEW dbo.vw_InventoryOnHand
AS
SELECT
    i.Id                                AS ItemId,
    i.Code                              AS ItemCode,
    i.Name                              AS ItemName,
    i.Unit,
    i.Category,
    CASE i.Type WHEN 1 THEN N'Nguyên vật liệu' WHEN 2 THEN N'Thành phẩm' ELSE N'Khác' END AS ItemTypeName,
    i.MinStock,
    i.StandardCost,
    w.Id                                AS WarehouseId,
    w.Code                              AS WarehouseCode,
    w.Name                              AS WarehouseName,
    ISNULL(SUM(t.Quantity), 0)          AS OnHandQuantity,
    ISNULL(SUM(t.Amount), 0)            AS StockValue,
    CASE
        WHEN ISNULL(SUM(t.Quantity), 0) <> 0
            THEN SUM(t.Amount) / SUM(t.Quantity)
        ELSE i.StandardCost
    END                                 AS AvgUnitCost,
    CASE WHEN ISNULL(SUM(t.Quantity), 0) < i.MinStock THEN 1 ELSE 0 END AS IsBelowMinStock
FROM dbo.Items i
CROSS JOIN dbo.Warehouses w
LEFT JOIN dbo.InventoryTransactions t
       ON t.ItemId = i.Id
      AND t.WarehouseId = w.Id
WHERE i.IsActive = 1
  AND w.IsActive = 1
GROUP BY
    i.Id, i.Code, i.Name, i.Unit, i.Category, i.Type, i.MinStock, i.StandardCost,
    w.Id, w.Code, w.Name;
GO

/* ============================================================
   2. Hiệu suất lệnh sản xuất: kế hoạch vs thực tế, tỷ lệ lỗi
   ============================================================ */
CREATE OR ALTER VIEW dbo.vw_ProductionPerformance
AS
SELECT
    po.Id                               AS ProductionOrderId,
    po.OrderNo,
    po.Status                           AS StatusCode,
    CASE po.Status
        WHEN 0 THEN N'Nháp'
        WHEN 1 THEN N'Chờ duyệt'
        WHEN 2 THEN N'Đã duyệt'
        WHEN 3 THEN N'Đang sản xuất'
        WHEN 4 THEN N'Hoàn thành'
        WHEN 5 THEN N'Đã hủy'
        ELSE N'Không xác định'
    END                                 AS StatusName,
    i.Id                                AS ProductItemId,
    i.Code                              AS ProductCode,
    i.Name                              AS ProductName,
    i.Unit,
    i.Category,
    b.Code                              AS BomCode,
    b.Version                           AS BomVersion,
    po.PlannedQuantity,
    po.ActualQuantity,
    po.DefectQuantity,
    CAST(
        CASE WHEN po.PlannedQuantity > 0
             THEN po.ActualQuantity * 100.0 / po.PlannedQuantity
             ELSE 0 END
        AS decimal(9,2))                AS CompletionRatePct,
    CAST(
        CASE WHEN po.ActualQuantity + po.DefectQuantity > 0
             THEN po.DefectQuantity * 100.0 / (po.ActualQuantity + po.DefectQuantity)
             ELSE 0 END
        AS decimal(9,2))                AS DefectRatePct,
    po.StandardMaterialCost,
    po.ActualMaterialCost,
    po.ActualMaterialCost - po.StandardMaterialCost AS MaterialCostVarianceAmount,
    CAST(
        CASE WHEN po.StandardMaterialCost > 0
             THEN (po.ActualMaterialCost - po.StandardMaterialCost) * 100.0 / po.StandardMaterialCost
             ELSE 0 END
        AS decimal(9,2))                AS MaterialCostVariancePct,
    po.StartDate,
    po.DueDate,
    CONVERT(varchar(7), po.DueDate, 126) AS DueMonth,
    po.CompletedAt
FROM dbo.ProductionOrders po
JOIN dbo.Items i ON i.Id = po.FinishedGoodItemId
JOIN dbo.Boms  b ON b.Id = po.BomId;
GO

/* ============================================================
   3. Chênh lệch chi phí nguyên vật liệu định mức vs thực tế
   ============================================================ */
CREATE OR ALTER VIEW dbo.vw_MaterialCostVariance
AS
SELECT
    po.Id                               AS ProductionOrderId,
    po.OrderNo,
    i.Code                              AS ProductCode,
    i.Name                              AS ProductName,
    po.PlannedQuantity,
    po.ActualQuantity,
    po.StandardMaterialCost,
    po.ActualMaterialCost,
    po.ActualMaterialCost - po.StandardMaterialCost AS VarianceAmount,
    CAST(
        CASE WHEN po.StandardMaterialCost > 0
             THEN (po.ActualMaterialCost - po.StandardMaterialCost) * 100.0 / po.StandardMaterialCost
             ELSE 0 END
        AS decimal(9,2))                AS VariancePct,
    CASE
        WHEN po.StandardMaterialCost = 0 THEN N'Chưa có định mức'
        WHEN po.ActualMaterialCost > po.StandardMaterialCost THEN N'Vượt định mức'
        WHEN po.ActualMaterialCost < po.StandardMaterialCost THEN N'Tiết kiệm'
        ELSE N'Đúng định mức'
    END                                 AS VarianceStatus,
    CAST(
        CASE WHEN po.ActualQuantity > 0
             THEN po.ActualMaterialCost / po.ActualQuantity
             ELSE 0 END
        AS decimal(18,4))               AS ActualMaterialCostPerUnit,
    po.StartDate,
    po.DueDate,
    CONVERT(varchar(7), po.DueDate, 126) AS DueMonth,
    po.CompletedAt
FROM dbo.ProductionOrders po
JOIN dbo.Items i ON i.Id = po.FinishedGoodItemId
WHERE po.Status <> 5;
GO

/* ============================================================
   4. Tiến độ lệnh sản xuất và cảnh báo trễ hạn
   ============================================================ */
CREATE OR ALTER VIEW dbo.vw_ProductionOrderProgress
AS
SELECT
    po.Id                               AS ProductionOrderId,
    po.OrderNo,
    po.Status                           AS StatusCode,
    CASE po.Status
        WHEN 0 THEN N'Nháp'
        WHEN 1 THEN N'Chờ duyệt'
        WHEN 2 THEN N'Đã duyệt'
        WHEN 3 THEN N'Đang sản xuất'
        WHEN 4 THEN N'Hoàn thành'
        WHEN 5 THEN N'Đã hủy'
        ELSE N'Không xác định'
    END                                 AS StatusName,
    i.Code                              AS ProductCode,
    i.Name                              AS ProductName,
    po.PlannedQuantity,
    po.ActualQuantity,
    CAST(
        CASE WHEN po.PlannedQuantity > 0
             THEN po.ActualQuantity * 100.0 / po.PlannedQuantity
             ELSE 0 END
        AS decimal(9,2))                AS ProgressPct,
    po.StartDate,
    po.DueDate,
    po.CompletedAt,
    CAST(GETDATE() AS date)             AS Today,
    DATEDIFF(day, po.DueDate, CAST(GETDATE() AS date)) AS DaysPastDue,
    CASE
        WHEN po.Status = 4 THEN 0
        WHEN po.Status = 5 THEN 0
        WHEN po.DueDate < CAST(GETDATE() AS date) THEN 1
        ELSE 0
    END                                 AS IsOverdue
FROM dbo.ProductionOrders po
JOIN dbo.Items i ON i.Id = po.FinishedGoodItemId;
GO

/* ============================================================
   5. Nhập — xuất — tồn theo tháng, vật tư và kho
   ============================================================ */
CREATE OR ALTER VIEW dbo.vw_StockMovement
AS
WITH Monthly AS (
    SELECT
        i.Id                            AS ItemId,
        i.Code                          AS ItemCode,
        i.Name                          AS ItemName,
        i.Unit,
        w.Id                            AS WarehouseId,
        w.Code                          AS WarehouseCode,
        w.Name                          AS WarehouseName,
        DATEFROMPARTS(YEAR(t.TransactionDate), MONTH(t.TransactionDate), 1) AS PeriodStart,
        SUM(CASE WHEN t.Quantity > 0 THEN t.Quantity ELSE 0 END)  AS ReceiptQuantity,
        SUM(CASE WHEN t.Quantity < 0 THEN -t.Quantity ELSE 0 END) AS IssueQuantity,
        SUM(t.Quantity)                 AS NetQuantity,
        SUM(t.Amount)                   AS NetValue
    FROM dbo.InventoryTransactions t
    JOIN dbo.Items i      ON i.Id = t.ItemId
    JOIN dbo.Warehouses w ON w.Id = t.WarehouseId
    GROUP BY
        i.Id, i.Code, i.Name, i.Unit,
        w.Id, w.Code, w.Name,
        DATEFROMPARTS(YEAR(t.TransactionDate), MONTH(t.TransactionDate), 1)
)
SELECT
    ItemId, ItemCode, ItemName, Unit,
    WarehouseId, WarehouseCode, WarehouseName,
    PeriodStart,
    CONVERT(varchar(7), PeriodStart, 126) AS PeriodLabel,
    ReceiptQuantity,
    IssueQuantity,
    NetQuantity,
    NetValue,
    SUM(NetQuantity) OVER (
        PARTITION BY ItemId, WarehouseId
        ORDER BY PeriodStart
        ROWS UNBOUNDED PRECEDING
    )                                   AS ClosingQuantity
FROM Monthly;
GO

/* ============================================================
   6. Sản lượng theo tháng, sản phẩm và dòng hàng
   ============================================================ */
CREATE OR ALTER VIEW dbo.vw_OutputMonthly
AS
SELECT
    DATEFROMPARTS(YEAR(o.OutputDate), MONTH(o.OutputDate), 1) AS PeriodStart,
    CONVERT(varchar(7), o.OutputDate, 126) AS PeriodLabel,
    i.Id                                AS ItemId,
    i.Code                              AS ProductCode,
    i.Name                              AS ProductName,
    i.Category,
    i.Unit,
    SUM(o.GoodQuantity)                 AS GoodQuantity,
    SUM(o.DefectQuantity)               AS DefectQuantity,
    SUM(o.GoodQuantity + o.DefectQuantity) AS TotalQuantity,
    CAST(
        CASE WHEN SUM(o.GoodQuantity + o.DefectQuantity) > 0
             THEN SUM(o.DefectQuantity) * 100.0 / SUM(o.GoodQuantity + o.DefectQuantity)
             ELSE 0 END
        AS decimal(9,2))                AS DefectRatePct,
    SUM(o.TotalCost)                    AS TotalCost,
    CAST(
        CASE WHEN SUM(o.GoodQuantity) > 0
             THEN SUM(o.TotalCost) / SUM(o.GoodQuantity)
             ELSE 0 END
        AS decimal(18,4))               AS UnitCost
FROM dbo.ProductionOutputs o
JOIN dbo.ProductionOrders po ON po.Id = o.ProductionOrderId
JOIN dbo.Items i             ON i.Id = po.FinishedGoodItemId
GROUP BY
    DATEFROMPARTS(YEAR(o.OutputDate), MONTH(o.OutputDate), 1),
    CONVERT(varchar(7), o.OutputDate, 126),
    i.Id, i.Code, i.Name, i.Category, i.Unit;
GO

PRINT N'Đã tạo 6 view báo cáo: vw_InventoryOnHand, vw_ProductionPerformance, vw_MaterialCostVariance, vw_ProductionOrderProgress, vw_StockMovement, vw_OutputMonthly';
GO
