using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Common;
using CvErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Infrastructure.Services;

/// <summary>Điều khiển luồng duyệt chứng từ: Nháp → Chờ duyệt → Đã duyệt → Ghi sổ.</summary>
public class WorkflowService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public WorkflowService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task SubmitStockDocumentAsync(int id, UserContext user)
    {
        var document = await _db.StockDocuments.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy chứng từ kho.");

        if (document.Status != DocumentStatus.Draft)
        {
            throw new BusinessRuleException("Chỉ chứng từ nháp mới được trình duyệt.");
        }

        document.Status = DocumentStatus.Submitted;
        document.SubmittedById = user.UserId;
        document.SubmittedAt = DateTime.UtcNow;

        await SaveAsync();
        await _audit.LogAsync(user, "SUBMIT", nameof(StockDocument), document.Id.ToString(),
            $"Trình duyệt chứng từ {document.DocumentNo}");
    }

    public async Task ApproveStockDocumentAsync(int id, UserContext user)
    {
        var document = await _db.StockDocuments.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy chứng từ kho.");

        if (document.Status != DocumentStatus.Submitted)
        {
            throw new BusinessRuleException("Chỉ chứng từ đang chờ duyệt mới được phê duyệt.");
        }

        document.Status = DocumentStatus.Approved;
        document.ApprovedById = user.UserId;
        document.ApprovedAt = DateTime.UtcNow;

        await SaveAsync();
        await _audit.LogAsync(user, "APPROVE", nameof(StockDocument), document.Id.ToString(),
            $"Phê duyệt chứng từ {document.DocumentNo}");
    }

    public async Task CancelStockDocumentAsync(int id, string? reason, UserContext user)
    {
        var document = await _db.StockDocuments.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy chứng từ kho.");

        if (document.Status == DocumentStatus.Posted)
        {
            throw new BusinessRuleException("Chứng từ đã ghi sổ, không thể hủy.");
        }

        if (document.Status == DocumentStatus.Cancelled)
        {
            throw new BusinessRuleException("Chứng từ đã được hủy trước đó.");
        }

        document.Status = DocumentStatus.Cancelled;
        document.CancelledById = user.UserId;
        document.CancelledAt = DateTime.UtcNow;
        document.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason.Trim();

        await SaveAsync();
        await _audit.LogAsync(user, "CANCEL", nameof(StockDocument), document.Id.ToString(),
            $"Hủy chứng từ {document.DocumentNo}: {document.CancelReason}");
    }

    public async Task SubmitMaterialIssueAsync(int id, UserContext user)
    {
        var issue = await _db.MaterialIssues.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy phiếu cấp phát.");

        if (issue.Status != DocumentStatus.Draft)
        {
            throw new BusinessRuleException("Chỉ phiếu nháp mới được trình duyệt.");
        }

        issue.Status = DocumentStatus.Submitted;
        issue.SubmittedById = user.UserId;
        issue.SubmittedAt = DateTime.UtcNow;

        await SaveAsync();
        await _audit.LogAsync(user, "SUBMIT", nameof(MaterialIssue), issue.Id.ToString(),
            $"Trình duyệt phiếu cấp phát {issue.IssueNo}");
    }

    public async Task ApproveMaterialIssueAsync(int id, UserContext user)
    {
        var issue = await _db.MaterialIssues.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy phiếu cấp phát.");

        if (issue.Status != DocumentStatus.Submitted)
        {
            throw new BusinessRuleException("Chỉ phiếu đang chờ duyệt mới được phê duyệt.");
        }

        issue.Status = DocumentStatus.Approved;
        issue.ApprovedById = user.UserId;
        issue.ApprovedAt = DateTime.UtcNow;

        await SaveAsync();
        await _audit.LogAsync(user, "APPROVE", nameof(MaterialIssue), issue.Id.ToString(),
            $"Phê duyệt phiếu cấp phát {issue.IssueNo}");
    }

    public async Task CancelMaterialIssueAsync(int id, string? reason, UserContext user)
    {
        var issue = await _db.MaterialIssues.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy phiếu cấp phát.");

        if (issue.Status == DocumentStatus.Posted)
        {
            throw new BusinessRuleException("Phiếu cấp phát đã ghi sổ, không thể hủy.");
        }

        if (issue.Status == DocumentStatus.Cancelled)
        {
            throw new BusinessRuleException("Phiếu cấp phát đã được hủy trước đó.");
        }

        issue.Status = DocumentStatus.Cancelled;
        issue.CancelledById = user.UserId;
        issue.CancelledAt = DateTime.UtcNow;
        issue.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason.Trim();

        await SaveAsync();
        await _audit.LogAsync(user, "CANCEL", nameof(MaterialIssue), issue.Id.ToString(),
            $"Hủy phiếu cấp phát {issue.IssueNo}: {issue.CancelReason}");
    }

    public async Task SubmitProductionOrderAsync(int id, UserContext user)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy lệnh sản xuất.");

        if (order.Status != ProductionOrderStatus.Draft)
        {
            throw new BusinessRuleException("Chỉ lệnh sản xuất nháp mới được trình duyệt.");
        }

        order.Status = ProductionOrderStatus.Submitted;
        order.SubmittedById = user.UserId;
        order.SubmittedAt = DateTime.UtcNow;

        await SaveAsync();
        await _audit.LogAsync(user, "SUBMIT", nameof(ProductionOrder), order.Id.ToString(),
            $"Trình duyệt lệnh sản xuất {order.OrderNo}");
    }

    public async Task ApproveProductionOrderAsync(int id, UserContext user)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy lệnh sản xuất.");

        if (order.Status != ProductionOrderStatus.Submitted)
        {
            throw new BusinessRuleException("Chỉ lệnh đang chờ duyệt mới được phê duyệt.");
        }

        order.Status = ProductionOrderStatus.Approved;
        order.ApprovedById = user.UserId;
        order.ApprovedAt = DateTime.UtcNow;

        await SaveAsync();
        await _audit.LogAsync(user, "APPROVE", nameof(ProductionOrder), order.Id.ToString(),
            $"Phê duyệt lệnh sản xuất {order.OrderNo}");
    }

    public async Task CancelProductionOrderAsync(int id, string? reason, UserContext user)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessRuleException("Không tìm thấy lệnh sản xuất.");

        if (order.Status is ProductionOrderStatus.InProgress or ProductionOrderStatus.Completed)
        {
            throw new BusinessRuleException("Lệnh đang sản xuất hoặc đã hoàn thành, không thể hủy.");
        }

        if (order.Status == ProductionOrderStatus.Cancelled)
        {
            throw new BusinessRuleException("Lệnh sản xuất đã được hủy trước đó.");
        }

        order.Status = ProductionOrderStatus.Cancelled;
        order.CancelledById = user.UserId;
        order.CancelledAt = DateTime.UtcNow;
        order.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason.Trim();

        await SaveAsync();
        await _audit.LogAsync(user, "CANCEL", nameof(ProductionOrder), order.Id.ToString(),
            $"Hủy lệnh sản xuất {order.OrderNo}: {order.CancelReason}");
    }

    private async Task SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Chứng từ vừa được người khác xử lý. Vui lòng tải lại trang.");
        }
    }
}
