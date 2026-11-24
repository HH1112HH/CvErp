using System.Data;
using System.Globalization;
using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Common;
using CvErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Infrastructure.Services;

public class InventoryService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public InventoryService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task PostStockDocumentAsync(int documentId, UserContext user)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var document = await _db.StockDocuments
            .Include(x => x.Lines)
            .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == documentId)
            ?? throw new BusinessRuleException("Không tìm thấy chứng từ kho.");

        if (document.Status != DocumentStatus.Approved)
        {
            throw new BusinessRuleException("Chỉ chứng từ đã duyệt mới được ghi sổ.");
        }

        if (document.Lines.Count == 0)
        {
            throw new BusinessRuleException("Chứng từ không có dòng vật tư.");
        }

        if (document.Type == StockDocumentType.Transfer)
        {
            if (document.DestinationWarehouseId is null)
            {
                throw new BusinessRuleException("Phiếu điều chuyển chưa chọn kho nhận.");
            }

            if (document.DestinationWarehouseId == document.WarehouseId)
            {
                throw new BusinessRuleException("Kho nhận phải khác kho xuất.");
            }
        }

        var lines = document.Lines;
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                throw new BusinessRuleException($"Số lượng của vật tư {line.Item?.Name} phải lớn hơn 0.");
            }

            if (line.UnitCost <= 0)
            {
                line.UnitCost = line.Item?.StandardCost ?? 0;
            }

            line.Amount = decimal.Round(line.Quantity * line.UnitCost, 4);
        }

        document.TotalAmount = lines.Sum(x => x.Amount);

        if (document.Type is StockDocumentType.Issue or StockDocumentType.Transfer)
        {
            await EnsureStockAvailableAsync(document.WarehouseId, lines
                .GroupBy(x => x.ItemId)
                .Select(g => new StockDemand(g.Key, g.Sum(x => x.Quantity), g.First().Item?.Name ?? $"#{g.Key}")));
        }

        var now = DateTime.UtcNow;
        foreach (var line in lines)
        {
            if (document.Type == StockDocumentType.Receipt)
            {
                AddTransaction(document, line, document.WarehouseId, line.Quantity, InventoryTransactionType.GoodsReceipt, user.UserId, now);
            }
            else if (document.Type == StockDocumentType.Issue)
            {
                AddTransaction(document, line, document.WarehouseId, -line.Quantity, InventoryTransactionType.GoodsIssue, user.UserId, now);
            }
            else
            {
                AddTransaction(document, line, document.WarehouseId, -line.Quantity, InventoryTransactionType.TransferOut, user.UserId, now);
                AddTransaction(document, line, document.DestinationWarehouseId!.Value, line.Quantity, InventoryTransactionType.TransferIn, user.UserId, now);
            }
        }

        document.Status = DocumentStatus.Posted;
        document.PostedAt = now;
        document.PostedById = user.UserId;

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Chứng từ vừa được người khác xử lý. Vui lòng tải lại trang.");
        }

        await _audit.LogAsync(user, "POST", nameof(StockDocument), document.Id.ToString(),
            $"Ghi sổ {TypeName(document.Type)} {document.DocumentNo}");
    }

    public async Task PostMaterialIssueAsync(int materialIssueId, UserContext user)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var issue = await _db.MaterialIssues
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.ProductionOrder)
            .FirstOrDefaultAsync(x => x.Id == materialIssueId)
            ?? throw new BusinessRuleException("Không tìm thấy phiếu cấp phát.");

        if (issue.Status != DocumentStatus.Approved)
        {
            throw new BusinessRuleException("Chỉ phiếu cấp phát đã duyệt mới được ghi sổ.");
        }

        var order = issue.ProductionOrder
            ?? throw new BusinessRuleException("Phiếu cấp phát chưa gắn lệnh sản xuất.");

        if (order.Status is not (ProductionOrderStatus.Approved or ProductionOrderStatus.InProgress))
        {
            throw new BusinessRuleException("Lệnh sản xuất phải ở trạng thái Đã duyệt hoặc Đang sản xuất mới được cấp phát vật tư.");
        }

        if (issue.Lines.Count == 0)
        {
            throw new BusinessRuleException("Phiếu cấp phát không có dòng vật tư.");
        }

        foreach (var line in issue.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new BusinessRuleException("Số lượng cấp phát phải lớn hơn 0.");
            }

            if (line.UnitCost <= 0)
            {
                line.UnitCost = line.Item?.StandardCost ?? 0;
            }

            line.Amount = decimal.Round(line.Quantity * line.UnitCost, 4);
        }

        await EnsureStockAvailableAsync(issue.WarehouseId, issue.Lines
            .GroupBy(x => x.ItemId)
            .Select(g => new StockDemand(g.Key, g.Sum(x => x.Quantity), g.First().Item?.Name ?? $"#{g.Key}")));

        var total = issue.Lines.Sum(x => x.Amount);
        var now = DateTime.UtcNow;
        foreach (var line in issue.Lines)
        {
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionDate = issue.IssueDate,
                ItemId = line.ItemId,
                WarehouseId = issue.WarehouseId,
                Type = InventoryTransactionType.ProductionIssue,
                Quantity = -line.Quantity,
                UnitCost = line.UnitCost,
                Amount = -line.Amount,
                ReferenceType = ReferenceType.MaterialIssue,
                ReferenceId = issue.Id,
                ReferenceNo = issue.IssueNo,
                Note = issue.Note,
                CreatedById = user.UserId,
                CreatedAt = now
            });
        }

        issue.TotalAmount = total;
        issue.Status = DocumentStatus.Posted;
        issue.PostedAt = now;
        issue.PostedById = user.UserId;

        order.ActualMaterialCost += total;
        if (order.Status == ProductionOrderStatus.Approved)
        {
            order.Status = ProductionOrderStatus.InProgress;
        }

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Phiếu cấp phát vừa được người khác xử lý. Vui lòng tải lại trang.");
        }

        await _audit.LogAsync(user, "POST", nameof(MaterialIssue), issue.Id.ToString(),
            $"Ghi sổ phiếu cấp phát {issue.IssueNo} cho lệnh {order.OrderNo}");
    }

    public async Task<List<InventoryTransaction>> GetLedgerAsync(int itemId, int warehouseId)
    {
        return await _db.InventoryTransactions
            .AsNoTracking()
            .Include(x => x.Item)
            .Include(x => x.Warehouse)
            .Where(x => x.ItemId == itemId && x.WarehouseId == warehouseId)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    internal async Task EnsureStockAvailableAsync(int warehouseId, IEnumerable<StockDemand> demands)
    {
        var list = demands.ToList();
        if (list.Count == 0)
        {
            return;
        }

        var itemIds = list.Select(x => x.ItemId).Distinct().ToList();
        var balances = await _db.InventoryTransactions
            .Where(x => x.WarehouseId == warehouseId && itemIds.Contains(x.ItemId))
            .GroupBy(x => x.ItemId)
            .Select(g => new { ItemId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Quantity);

        foreach (var demand in list)
        {
            balances.TryGetValue(demand.ItemId, out var available);
            if (available < demand.Quantity)
            {
                throw new BusinessRuleException(
                    $"Không đủ tồn kho cho \"{demand.ItemName}\". Khả dụng {available.ToString("N4", Vi)}, cần {demand.Quantity.ToString("N4", Vi)}.");
            }
        }
    }

    private void AddTransaction(StockDocument document, StockDocumentLine line, int warehouseId,
        decimal quantity, InventoryTransactionType type, string userId, DateTime now)
    {
        var amount = decimal.Round(quantity * line.UnitCost, 4);
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionDate = document.DocumentDate,
            ItemId = line.ItemId,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = quantity,
            UnitCost = line.UnitCost,
            Amount = amount,
            ReferenceType = ReferenceType.StockDocument,
            ReferenceId = document.Id,
            ReferenceNo = document.DocumentNo,
            Note = document.Note,
            CreatedById = userId,
            CreatedAt = now
        });
    }

    private static string TypeName(StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => "phiếu nhập kho",
        StockDocumentType.Issue => "phiếu xuất kho",
        _ => "phiếu điều chuyển"
    };
}

internal readonly record struct StockDemand(int ItemId, decimal Quantity, string ItemName);
