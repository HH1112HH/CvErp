using System.ComponentModel.DataAnnotations;
using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Web.ViewModels.Common;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvErp.Web.ViewModels.Inventory;

public class StockDocumentListViewModel
{
    public StockDocumentType? Type { get; set; }
    public DocumentStatus? Status { get; set; }
    public string? Search { get; set; }
    public int? WarehouseId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public List<SelectListItem> WarehouseOptions { get; set; } = new();
    public PagedResult<StockDocument> Documents { get; set; } = PagedResult<StockDocument>.Empty();
}

public class StockDocumentEditViewModel
{
    public int Id { get; set; }
    public string? DocumentNo { get; set; }
    public bool IsEdit => Id > 0;

    [Display(Name = "Loại chứng từ")]
    public StockDocumentType Type { get; set; } = StockDocumentType.Receipt;

    [Required(ErrorMessage = "Vui lòng chọn ngày chứng từ.")]
    [Display(Name = "Ngày chứng từ")]
    public DateOnly DocumentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn kho.")]
    [Display(Name = "Kho")]
    public int WarehouseId { get; set; }

    [Display(Name = "Kho nhận")]
    public int? DestinationWarehouseId { get; set; }

    [Display(Name = "Nhà cung cấp")]
    public int? SupplierId { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public List<StockDocumentLineEditModel> Lines { get; set; } = new();
    public List<SelectListItem> WarehouseOptions { get; set; } = new();
    public List<SelectListItem> SupplierOptions { get; set; } = new();
    public List<SelectListItem> ItemOptions { get; set; } = new();
}

public class StockDocumentLineEditModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn vật tư.")]
    public int ItemId { get; set; }

    [Range(0.0001, 999999999, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal Quantity { get; set; }

    [Range(0, 999999999, ErrorMessage = "Đơn giá không được âm.")]
    public decimal UnitCost { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}

public class StockDocumentDetailsViewModel
{
    public StockDocument Document { get; set; } = new();
    public Dictionary<string, string> UserNames { get; set; } = new();
}

public class StockBalanceViewModel
{
    public string? Search { get; set; }
    public int? WarehouseId { get; set; }
    public string? Category { get; set; }
    public bool LowStockOnly { get; set; }
    public List<SelectListItem> WarehouseOptions { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public PagedResult<StockBalanceRow> Rows { get; set; } = PagedResult<StockBalanceRow>.Empty();
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public int LowStockCount { get; set; }
    public int ItemCount { get; set; }
}

public class StockBalanceRow
{
    public int ItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Value { get; set; }
    public decimal MinStock { get; set; }
    public decimal TotalQuantity { get; set; }
    public bool IsLowStock => MinStock > 0 && TotalQuantity < MinStock;
}

public class StockCardViewModel
{
    public Item? Item { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int ItemId { get; set; }
    public int WarehouseId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningValue { get; set; }
    public decimal ClosingQuantity { get; set; }
    public decimal ClosingValue { get; set; }
    public List<StockCardRow> Rows { get; set; } = new();
    public List<SelectListItem> ItemOptions { get; set; } = new();
    public List<SelectListItem> WarehouseOptions { get; set; } = new();
}

public class StockCardRow
{
    public long Id { get; set; }
    public DateOnly Date { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public InventoryTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public decimal RunningQuantity { get; set; }
    public string? Note { get; set; }
}
