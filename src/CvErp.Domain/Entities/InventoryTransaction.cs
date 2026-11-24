using CvErp.Domain.Enums;

namespace CvErp.Domain.Entities;

/// <summary>
/// Sổ cái kho: nguồn dữ liệu duy nhất cho tồn kho. Quantity âm là xuất, dương là nhập.
/// </summary>
public class InventoryTransaction
{
    public long Id { get; set; }
    public DateOnly TransactionDate { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public InventoryTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public ReferenceType ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
