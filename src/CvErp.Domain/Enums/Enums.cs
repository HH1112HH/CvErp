namespace CvErp.Domain.Enums;

public enum ItemType
{
    RawMaterial = 1,
    FinishedGood = 2
}

public enum StockDocumentType
{
    Receipt = 1,
    Issue = 2,
    Transfer = 3
}

/// <summary>Trạng thái dùng chung cho chứng từ kho và phiếu cấp phát vật tư.</summary>
public enum DocumentStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Posted = 3,
    Cancelled = 4
}

public enum ProductionOrderStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}

public enum InventoryTransactionType
{
    Opening = 1,
    GoodsReceipt = 2,
    GoodsIssue = 3,
    TransferIn = 4,
    TransferOut = 5,
    ProductionIssue = 6,
    ProductionReceipt = 7,
    AdjustmentIn = 8,
    AdjustmentOut = 9
}

public enum ReferenceType
{
    Opening = 1,
    StockDocument = 2,
    MaterialIssue = 3,
    ProductionOrder = 4,
    Adjustment = 5
}
