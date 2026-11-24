namespace CvErp.Domain.Entities;

/// <summary>Sản lượng ghi nhận khi hoàn thành lệnh sản xuất.</summary>
public class ProductionOutput
{
    public int Id { get; set; }
    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public DateOnly OutputDate { get; set; }
    public decimal GoodQuantity { get; set; }
    public decimal DefectQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Note { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
