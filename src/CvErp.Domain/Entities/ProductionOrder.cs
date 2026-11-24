using CvErp.Domain.Enums;

namespace CvErp.Domain.Entities;

/// <summary>Lệnh sản xuất thành phẩm theo một BOM.</summary>
public class ProductionOrder
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public int FinishedGoodItemId { get; set; }
    public Item? FinishedGoodItem { get; set; }
    public int BomId { get; set; }
    public Bom? Bom { get; set; }
    public int OutputWarehouseId { get; set; }
    public Warehouse? OutputWarehouse { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal DefectQuantity { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Draft;
    public string? Note { get; set; }
    public decimal StandardMaterialCost { get; set; }
    public decimal ActualMaterialCost { get; set; }

    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? CancelledById { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<ProductionOrderMaterial> Materials { get; set; } = new();
    public List<MaterialIssue> MaterialIssues { get; set; } = new();
    public List<ProductionOutput> Outputs { get; set; } = new();
}

/// <summary>Ảnh chụp nhu cầu nguyên vật liệu tại thời điểm tạo lệnh sản xuất.</summary>
public class ProductionOrderMaterial
{
    public int Id { get; set; }
    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal StandardQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal StandardAmount { get; set; }
}
