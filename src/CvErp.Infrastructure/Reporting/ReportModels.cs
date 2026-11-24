using CvErp.Domain.Enums;

namespace CvErp.Infrastructure.Reporting;

public record LowStockRow(int ItemId, string Code, string Name, string Unit, decimal OnHand, decimal MinStock, decimal Shortage);

public record StockByWarehouseRow(string WarehouseName, decimal Quantity, decimal Value);

public record TopConsumedRow(string Code, string Name, string Unit, decimal Quantity, decimal Amount);

public record StockFlowPoint(string Month, decimal ReceiptValue, decimal IssueValue);

public record RecentTransactionRow(long Id, DateOnly Date, string ReferenceNo, string ItemName, string WarehouseName,
    InventoryTransactionType Type, decimal Quantity, decimal Amount);

public sealed class InventoryOverview
{
    public decimal TotalStockValue { get; init; }
    public int ItemCount { get; init; }
    public int RawMaterialCount { get; init; }
    public int LowStockCount { get; init; }
    public List<StockFlowPoint> StockFlow { get; init; } = new();
    public List<TopConsumedRow> TopConsumed { get; init; } = new();
    public List<StockByWarehouseRow> StockByWarehouse { get; init; } = new();
    public List<LowStockRow> LowStock { get; init; } = new();
    public List<RecentTransactionRow> RecentTransactions { get; init; } = new();
}

public record ProductionOrderSummary(int Id, string OrderNo, string ProductCode, string ProductName,
    decimal PlannedQuantity, decimal ActualQuantity, decimal DefectQuantity, ProductionOrderStatus Status,
    DateOnly StartDate, DateOnly DueDate, DateTime? CompletedAt);

public record MonthlyOutputPoint(string Month, decimal Planned, decimal Actual, decimal Defect);

public record StatusCount(ProductionOrderStatus Status, int Count);

public sealed class ProductionOverview
{
    public decimal PlannedInPeriod { get; init; }
    public decimal ActualInPeriod { get; init; }
    public decimal DefectInPeriod { get; init; }
    public decimal CompletionRate { get; init; }
    public decimal DefectRate { get; init; }
    public int InProgressCount { get; init; }
    public int PendingApprovalCount { get; init; }
    public int CompletedOrderCount { get; init; }
    public List<MonthlyOutputPoint> OutputByMonth { get; init; } = new();
    public List<StatusCount> StatusCounts { get; init; } = new();
    public List<ProductionOrderSummary> UpcomingOrders { get; init; } = new();
    public List<ProductionOrderSummary> RecentOrders { get; init; } = new();
}

public record MaterialVarianceRow(string Code, string Name, string Unit,
    decimal StandardQuantity, decimal ActualQuantity, decimal StandardAmount, decimal ActualAmount)
{
    public decimal QuantityVariance => ActualQuantity - StandardQuantity;
    public decimal AmountVariance => ActualAmount - StandardAmount;
    public decimal VariancePercent => StandardAmount == 0 ? (ActualAmount == 0 ? 0 : 100) : AmountVariance / StandardAmount * 100;
}

public record OrderVarianceRow(int Id, string OrderNo, string ProductName, decimal PlannedQuantity,
    decimal StandardCost, decimal ActualCost, ProductionOrderStatus Status)
{
    public decimal Variance => ActualCost - StandardCost;
    public decimal VariancePercent => StandardCost == 0 ? (ActualCost == 0 ? 0 : 100) : Variance / StandardCost * 100;
}

public record MonthlyVariancePoint(string Month, decimal StandardCost, decimal ActualCost);

public sealed class VarianceOverview
{
    public decimal StandardCost { get; init; }
    public decimal ActualCost { get; init; }
    public decimal Variance { get; init; }
    public decimal VariancePercent { get; init; }
    public int OrderCount { get; init; }
    public int OverrunItemCount { get; init; }
    public List<MaterialVarianceRow> TopOverruns { get; init; } = new();
    public List<OrderVarianceRow> Orders { get; init; } = new();
    public List<MonthlyVariancePoint> Monthly { get; init; } = new();
}
