using CvErp.Domain.Enums;

namespace CvErp.Domain.Entities;

/// <summary>Phiếu cấp phát nguyên vật liệu cho lệnh sản xuất.</summary>
public class MaterialIssue
{
    public int Id { get; set; }
    public string IssueNo { get; set; } = string.Empty;
    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public DateOnly IssueDate { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }

    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? PostedById { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? CancelledById { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<MaterialIssueLine> Lines { get; set; } = new();
}

public class MaterialIssueLine
{
    public int Id { get; set; }
    public int MaterialIssueId { get; set; }
    public MaterialIssue? MaterialIssue { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
