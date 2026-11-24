using System.ComponentModel.DataAnnotations;
using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Web.ViewModels.Common;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvErp.Web.ViewModels.MasterData;

public class ItemListViewModel
{
    public string? Search { get; set; }
    public ItemType? Type { get; set; }
    public string? Category { get; set; }
    public bool? Active { get; set; }
    public List<string> Categories { get; set; } = new();
    public PagedResult<Item> Items { get; set; } = PagedResult<Item>.Empty();
}

public class ItemEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã vật tư.")]
    [StringLength(30, ErrorMessage = "Mã tối đa 30 ký tự.")]
    [Display(Name = "Mã vật tư")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên vật tư.")]
    [StringLength(200, ErrorMessage = "Tên tối đa 200 ký tự.")]
    [Display(Name = "Tên vật tư")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đơn vị tính.")]
    [StringLength(30)]
    [Display(Name = "Đơn vị tính")]
    public string Unit { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Nhóm vật tư")]
    public string Category { get; set; } = string.Empty;

    [Display(Name = "Loại")]
    public ItemType Type { get; set; } = ItemType.RawMaterial;

    [Range(0, 999999999, ErrorMessage = "Tồn tối thiểu không được âm.")]
    [Display(Name = "Tồn tối thiểu")]
    public decimal MinStock { get; set; }

    [Range(0, 999999999, ErrorMessage = "Đơn giá không được âm.")]
    [Display(Name = "Đơn giá chuẩn")]
    public decimal StandardCost { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id > 0;
}

public class WarehouseListViewModel
{
    public string? Search { get; set; }
    public bool? Active { get; set; }
    public List<Warehouse> Warehouses { get; set; } = new();
    public Dictionary<int, decimal> StockValues { get; set; } = new();
    public Dictionary<int, int> ItemCounts { get; set; } = new();
}

public class WarehouseEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã kho.")]
    [StringLength(30)]
    [Display(Name = "Mã kho")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên kho.")]
    [StringLength(150)]
    [Display(Name = "Tên kho")]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    [Display(Name = "Vị trí")]
    public string Location { get; set; } = string.Empty;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id > 0;
}

public class SupplierListViewModel
{
    public string? Search { get; set; }
    public bool? Active { get; set; }
    public PagedResult<Supplier> Suppliers { get; set; } = PagedResult<Supplier>.Empty();
}

public class SupplierEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã nhà cung cấp.")]
    [StringLength(30)]
    [Display(Name = "Mã nhà cung cấp")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên nhà cung cấp.")]
    [StringLength(200)]
    [Display(Name = "Tên nhà cung cấp")]
    public string Name { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Mã số thuế")]
    public string TaxCode { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Đang hợp tác")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id > 0;
}

public class BomListViewModel
{
    public string? Search { get; set; }
    public bool? Active { get; set; }
    public PagedResult<Bom> Boms { get; set; } = PagedResult<Bom>.Empty();
}

public class BomEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã BOM.")]
    [StringLength(30)]
    [Display(Name = "Mã BOM")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên BOM.")]
    [StringLength(200)]
    [Display(Name = "Tên BOM")]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thành phẩm.")]
    [Display(Name = "Thành phẩm")]
    public int FinishedGoodItemId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập phiên bản.")]
    [StringLength(20)]
    [Display(Name = "Phiên bản")]
    public string Version { get; set; } = "1.0";

    [Display(Name = "Đang áp dụng")]
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public List<BomLineEditModel> Lines { get; set; } = new();
    public List<SelectListItem> ProductOptions { get; set; } = new();
    public List<SelectListItem> ComponentOptions { get; set; } = new();

    public bool IsEdit => Id > 0;
}

public class BomLineEditModel
{
    [Range(0, int.MaxValue, ErrorMessage = "Vui lòng chọn nguyên vật liệu.")]
    public int ComponentItemId { get; set; }

    [Range(0.0001, 999999999, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal QuantityPerUnit { get; set; }

    [Range(0, 100, ErrorMessage = "Tỷ lệ hao hụt từ 0 đến 100%.")]
    public decimal ScrapPercent { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}
