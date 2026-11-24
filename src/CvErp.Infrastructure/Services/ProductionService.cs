using System.Data;
using System.Globalization;
using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Common;
using CvErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Infrastructure.Services;

public class ProductionService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public ProductionService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task CompleteProductionOrderAsync(int orderId, decimal goodQuantity, decimal defectQuantity,
        string? note, UserContext user)
    {
        if (goodQuantity < 0 || defectQuantity < 0)
        {
            throw new BusinessRuleException("Sản lượng không được âm.");
        }

        if (goodQuantity + defectQuantity <= 0)
        {
            throw new BusinessRuleException("Tổng sản lượng phải lớn hơn 0.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var order = await _db.ProductionOrders
            .Include(x => x.FinishedGoodItem)
            .Include(x => x.OutputWarehouse)
            .Include(x => x.Materials)
            .Include(x => x.MaterialIssues).ThenInclude(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId)
            ?? throw new BusinessRuleException("Không tìm thấy lệnh sản xuất.");

        if (order.Status is not (ProductionOrderStatus.Approved or ProductionOrderStatus.InProgress))
        {
            throw new BusinessRuleException("Chỉ lệnh đã duyệt hoặc đang sản xuất mới được ghi nhận hoàn thành.");
        }

        var postedIssues = order.MaterialIssues.Where(x => x.Status == DocumentStatus.Posted).ToList();
        if (postedIssues.Count == 0)
        {
            throw new BusinessRuleException("Chưa có phiếu cấp phát nào được ghi sổ. Hãy cấp phát vật tư trước khi hoàn thành.");
        }

        var actualCost = postedIssues.Sum(x => x.TotalAmount);
        var unitCost = goodQuantity > 0 ? decimal.Round(actualCost / goodQuantity, 4) : 0m;
        var now = DateTime.UtcNow;

        _db.ProductionOutputs.Add(new ProductionOutput
        {
            ProductionOrderId = order.Id,
            OutputDate = DateOnly.FromDateTime(DateTime.Today),
            GoodQuantity = goodQuantity,
            DefectQuantity = defectQuantity,
            UnitCost = unitCost,
            TotalCost = actualCost,
            Note = note,
            CreatedById = user.UserId,
            CreatedAt = now
        });

        if (goodQuantity > 0)
        {
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                ItemId = order.FinishedGoodItemId,
                WarehouseId = order.OutputWarehouseId,
                Type = InventoryTransactionType.ProductionReceipt,
                Quantity = goodQuantity,
                UnitCost = unitCost,
                Amount = actualCost,
                ReferenceType = ReferenceType.ProductionOrder,
                ReferenceId = order.Id,
                ReferenceNo = order.OrderNo,
                Note = string.IsNullOrWhiteSpace(note) ? $"Nhập thành phẩm từ lệnh {order.OrderNo}" : note,
                CreatedById = user.UserId,
                CreatedAt = now
            });
        }

        order.ActualQuantity = goodQuantity;
        order.DefectQuantity = defectQuantity;
        order.ActualMaterialCost = actualCost;
        order.Status = ProductionOrderStatus.Completed;
        order.CompletedAt = now;

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Lệnh sản xuất vừa được người khác xử lý. Vui lòng tải lại trang.");
        }

        await _audit.LogAsync(user, "COMPLETE", nameof(ProductionOrder), order.Id.ToString(),
            $"Hoàn thành lệnh {order.OrderNo}: {goodQuantity.ToString("N4", Vi)} tốt, {defectQuantity.ToString("N4", Vi)} lỗi, chi phí {actualCost.ToString("N0", Vi)} đ");
    }
}
