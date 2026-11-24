using System.ComponentModel.DataAnnotations;
using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Web.ViewModels.Common;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvErp.Web.ViewModels.Production;

public class ProductionOrderListViewModel
{
    public ProductionOrderStatus? Status { get; set; }
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public PagedResult<ProductionOrder> Orders { get; set; } = PagedResult<ProductionOrder>.Empty();
}

public class ProductionOrderCreateViewModel
{
    public int Id { get; set; }
    public string? OrderNo { get; set; }
    public bool IsEdit => Id > 0;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thành phẩm.")]
    [Display(Name = "Thành phẩm")]
    public int FinishedGoodItemId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn kho nhập thành phẩm.")]
    [Display(Name = "Kho nhập thành phẩm")]
    public int OutputWarehouseId { get; set; }

    [Range(0.0001, 999999999, ErrorMessage = "Số lượng kế hoạch phải lớn hơn 0.")]
    [Display(Name = "Số lượng kế hoạch")]
    public decimal PlannedQuantity { get; set; } = 10;

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
    [Display(Name = "Ngày bắt đầu")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Vui lòng chọn ngày hoàn thành.")]
    [Display(Name = "Ngày hoàn thành dự kiến")]
    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Today).AddDays(14);

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public List<SelectListItem> ProductOptions { get; set; } = new();
    public List<SelectListItem> WarehouseOptions { get; set; } = new();
    public List<ProductionOrderMaterial> ExistingMaterials { get; set; } = new();
}

public class ProductionOrderDetailsViewModel
{
    public ProductionOrder Order { get; set; } = new();
    public List<MaterialRequirementRow> Requirements { get; set; } = new();
    public Dictionary<string, string> UserNames { get; set; } = new();
    public List<MaterialIssue> MaterialIssues { get; set; } = new();
    public decimal StandardCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal IssuedStandardQuantity { get; set; }
    public decimal IssuedActualQuantity { get; set; }
}

public class MaterialRequirementRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal StandardQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal StandardAmount { get; set; }
    public decimal IssuedAmount { get; set; }
    public decimal QuantityVariance => IssuedQuantity - StandardQuantity;
    public bool IsOverrun => IssuedQuantity > StandardQuantity;
}

public class CompleteProductionOrderViewModel
{
    public int ProductionOrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }

    [Range(0, 999999999, ErrorMessage = "Sản lượng tốt không được âm.")]
    [Display(Name = "Sản lượng tốt (nhập kho)")]
    public decimal GoodQuantity { get; set; }

    [Range(0, 999999999, ErrorMessage = "Sản lượng lỗi không được âm.")]
    [Display(Name = "Sản lượng lỗi")]
    public decimal DefectQuantity { get; set; }

    [StringLength(400)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }
}

public class MaterialIssueListViewModel
{
    public DocumentStatus? Status { get; set; }
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? ProductionOrderId { get; set; }
    public PagedResult<MaterialIssue> Issues { get; set; } = PagedResult<MaterialIssue>.Empty();
}

public class MaterialIssueEditViewModel
{
    public int Id { get; set; }
    public string? IssueNo { get; set; }
    public bool IsEdit => Id > 0;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn lệnh sản xuất.")]
    [Display(Name = "Lệnh sản xuất")]
    public int ProductionOrderId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn kho xuất.")]
    [Display(Name = "Kho xuất")]
    public int WarehouseId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày cấp phát.")]
    [Display(Name = "Ngày cấp phát")]
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public List<MaterialIssueLineEditModel> Lines { get; set; } = new();
    public List<SelectListItem> OrderOptions { get; set; } = new();
    public List<SelectListItem> WarehouseOptions { get; set; } = new();
    public List<SelectListItem> ItemOptions { get; set; } = new();
    public int? ReturnOrderId { get; set; }
}

public class MaterialIssueLineEditModel
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

public class MaterialIssueDetailsViewModel
{
    public MaterialIssue Issue { get; set; } = new();
    public Dictionary<string, string> UserNames { get; set; } = new();
    public List<MaterialRequirementRow> Requirements { get; set; } = new();
}
