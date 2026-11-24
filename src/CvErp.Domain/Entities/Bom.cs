namespace CvErp.Domain.Entities;

/// <summary>Định mức nguyên vật liệu (BOM) cho một thành phẩm.</summary>
public class Bom
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int FinishedGoodItemId { get; set; }
    public Item? FinishedGoodItem { get; set; }
    public string Version { get; set; } = "1.0";
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BomLine> Lines { get; set; } = new();
}

public class BomLine
{
    public int Id { get; set; }
    public int BomId { get; set; }
    public Bom? Bom { get; set; }
    public int ComponentItemId { get; set; }
    public Item? ComponentItem { get; set; }
    public decimal QuantityPerUnit { get; set; }
    public decimal ScrapPercent { get; set; }
    public string? Note { get; set; }
}
