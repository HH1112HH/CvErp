using CvErp.Domain.Enums;

namespace CvErp.Domain.Entities;

/// <summary>Vật tư (nguyên vật liệu) hoặc thành phẩm.</summary>
public class Item
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ItemType Type { get; set; } = ItemType.RawMaterial;
    public decimal MinStock { get; set; }
    public decimal StandardCost { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BomLine> BomLines { get; set; } = new List<BomLine>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}
