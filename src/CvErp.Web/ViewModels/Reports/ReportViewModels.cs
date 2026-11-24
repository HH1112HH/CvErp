using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Reporting;
using CvErp.Web.ViewModels.Common;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvErp.Web.ViewModels.Reports;

public class StockSummaryReportViewModel
{
    public int? WarehouseId { get; set; }
    public string? Category { get; set; }
    public List<SelectListItem> WarehouseOptions { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<StockSummaryRow> Rows { get; set; } = new();
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public int ItemCount { get; set; }
    public int LowStockCount { get; set; }
}

public class StockSummaryRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    public decimal QuantityByWarehouse { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Value { get; set; }
    public decimal MinStock { get; set; }
    public bool IsLowStock => MinStock > 0 && TotalQuantity < MinStock;
}

public class MovementReportViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int? WarehouseId { get; set; }
    public int? ItemId { get; set; }
    public InventoryTransactionType? Type { get; set; }
    public List<SelectListItem> WarehouseOptions { get; set; } = new();
    public List<SelectListItem> ItemOptions { get; set; } = new();
    public PagedResult<InventoryTransaction> Transactions { get; set; } = PagedResult<InventoryTransaction>.Empty();
    public decimal TotalInQuantity { get; set; }
    public decimal TotalOutQuantity { get; set; }
    public decimal TotalInValue { get; set; }
    public decimal TotalOutValue { get; set; }
}

public class ProductionReportViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public ProductionOverview Data { get; set; } = new();
}

public class LowStockReportViewModel
{
    public List<LowStockRow> Rows { get; set; } = new();
    public int ItemCount { get; set; }
    public decimal TotalShortageValue { get; set; }
}

public class VarianceReportViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public VarianceOverview Data { get; set; } = new();
}
