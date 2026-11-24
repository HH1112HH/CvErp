using CvErp.Domain.Enums;

namespace CvErp.Domain.Entities;

/// <summary>Phiếu nhập kho, xuất kho hoặc điều chuyển kho.</summary>
public class StockDocument
{
    public int Id { get; set; }
    public string DocumentNo { get; set; } = string.Empty;
    public StockDocumentType Type { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DateOnly DocumentDate { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public int? DestinationWarehouseId { get; set; }
    public Warehouse? DestinationWarehouse { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

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
    public List<StockDocumentLine> Lines { get; set; } = new();
}

public class StockDocumentLine
{
    public int Id { get; set; }
    public int StockDocumentId { get; set; }
    public StockDocument? StockDocument { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
