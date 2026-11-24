using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Common;
using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Services;
using CvErp.Web.Helpers;
using CvErp.Web.ViewModels.Common;
using CvErp.Web.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

public class StockDocumentsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly NumberSequenceService _numbers;
    private readonly WorkflowService _workflow;
    private readonly InventoryService _inventory;
    private readonly AuditService _audit;

    public StockDocumentsController(AppDbContext db, NumberSequenceService numbers, WorkflowService workflow,
        InventoryService inventory, AuditService audit)
    {
        _db = db;
        _numbers = numbers;
        _workflow = workflow;
        _inventory = inventory;
        _audit = audit;
    }

    public async Task<IActionResult> Index(StockDocumentType? type, DocumentStatus? status, string? search,
        int? warehouseId, DateOnly? from, DateOnly? to, int page = 1)
    {
        var query = _db.StockDocuments.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Supplier)
            .AsQueryable();

        if (type is not null)
        {
            query = query.Where(x => x.Type == type);
        }

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.DocumentNo.Contains(keyword) || (x.Note != null && x.Note.Contains(keyword)));
        }

        if (warehouseId is not null)
        {
            query = query.Where(x => x.WarehouseId == warehouseId || x.DestinationWarehouseId == warehouseId);
        }

        if (from is not null)
        {
            query = query.Where(x => x.DocumentDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(x => x.DocumentDate <= to);
        }

        var model = new StockDocumentListViewModel
        {
            Type = type,
            Status = status,
            Search = search,
            WarehouseId = warehouseId,
            From = from,
            To = to,
            WarehouseOptions = await ErpLookups.WarehouseOptionsAsync(_db),
            Documents = await query.OrderByDescending(x => x.DocumentDate)
                .ThenByDescending(x => x.Id)
                .ToPagedResultAsync(page, 15)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var document = await _db.StockDocuments.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
            .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        var users = await ErpLookups.UserNamesAsync(_db, new[]
        {
            document.CreatedById, document.SubmittedById, document.ApprovedById,
            document.PostedById, document.CancelledById
        });

        return View(new StockDocumentDetailsViewModel { Document = document, UserNames = users });
    }

    [Authorize(Roles = "Administrator,Warehouse,Purchasing")]
    [HttpGet]
    public async Task<IActionResult> Create(StockDocumentType type = StockDocumentType.Receipt)
    {
        if (!Enum.IsDefined(type))
        {
            type = StockDocumentType.Receipt;
        }

        var model = new StockDocumentEditViewModel
        {
            Type = type,
            DocumentDate = DateOnly.FromDateTime(DateTime.Today),
            Lines = new List<StockDocumentLineEditModel> { new() { Quantity = 1 } }
        };

        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Administrator,Warehouse,Purchasing")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StockDocumentEditViewModel model)
    {
        model.Lines ??= new List<StockDocumentLineEditModel>();
        await ValidateDocumentAsync(model);

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model);
            return View(model);
        }

        var prefix = model.Type switch
        {
            StockDocumentType.Receipt => DocumentPrefixes.Receipt,
            StockDocumentType.Issue => DocumentPrefixes.Issue,
            _ => DocumentPrefixes.Transfer
        };

        var document = new StockDocument
        {
            DocumentNo = await _numbers.NextAsync(prefix, model.DocumentDate),
            Type = model.Type,
            Status = DocumentStatus.Draft,
            DocumentDate = model.DocumentDate,
            WarehouseId = model.WarehouseId,
            DestinationWarehouseId = model.Type == StockDocumentType.Transfer ? model.DestinationWarehouseId : null,
            SupplierId = model.Type == StockDocumentType.Receipt ? model.SupplierId : null,
            Note = model.Note?.Trim(),
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow
        };

        var items = await _db.Items.Where(x => model.Lines.Select(l => l.ItemId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        foreach (var line in model.Lines.Where(x => x.Quantity > 0 && x.ItemId > 0))
        {
            var unitCost = line.UnitCost > 0 ? line.UnitCost : items[line.ItemId].StandardCost;
            document.Lines.Add(new StockDocumentLine
            {
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                Amount = decimal.Round(line.Quantity * unitCost, 4),
                Note = line.Note?.Trim()
            });
        }

        document.TotalAmount = document.Lines.Sum(x => x.Amount);
        _db.StockDocuments.Add(document);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "CREATE", nameof(StockDocument), document.Id.ToString(),
            $"Tạo {UiText.DocTypeLabel(document.Type).ToLowerInvariant()} {document.DocumentNo}");

        Success($"Đã tạo chứng từ {document.DocumentNo} ở trạng thái Nháp.");
        return RedirectToAction(nameof(Details), new { id = document.Id });
    }

    [Authorize(Roles = "Administrator,Warehouse,Purchasing")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var document = await _db.StockDocuments.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        if (document.Status != DocumentStatus.Draft)
        {
            Error("Chỉ chứng từ nháp mới được chỉnh sửa.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new StockDocumentEditViewModel
        {
            Id = document.Id,
            DocumentNo = document.DocumentNo,
            Type = document.Type,
            DocumentDate = document.DocumentDate,
            WarehouseId = document.WarehouseId,
            DestinationWarehouseId = document.DestinationWarehouseId,
            SupplierId = document.SupplierId,
            Note = document.Note,
            Lines = document.Lines.Select(x => new StockDocumentLineEditModel
            {
                ItemId = x.ItemId,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost,
                Note = x.Note
            }).ToList()
        };

        if (model.Lines.Count == 0)
        {
            model.Lines.Add(new StockDocumentLineEditModel { Quantity = 1 });
        }

        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Administrator,Warehouse,Purchasing")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StockDocumentEditViewModel model)
    {
        var document = await _db.StockDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (document is null)
        {
            return NotFound();
        }

        if (document.Status != DocumentStatus.Draft)
        {
            Error("Chỉ chứng từ nháp mới được chỉnh sửa.");
            return RedirectToAction(nameof(Details), new { id });
        }

        model.Type = document.Type;
        model.Lines ??= new List<StockDocumentLineEditModel>();
        await ValidateDocumentAsync(model);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.DocumentNo = document.DocumentNo;
            await FillOptionsAsync(model);
            return View(model);
        }

        document.DocumentDate = model.DocumentDate;
        document.WarehouseId = model.WarehouseId;
        document.DestinationWarehouseId = document.Type == StockDocumentType.Transfer ? model.DestinationWarehouseId : null;
        document.SupplierId = document.Type == StockDocumentType.Receipt ? model.SupplierId : null;
        document.Note = model.Note?.Trim();

        _db.StockDocumentLines.RemoveRange(document.Lines);
        document.Lines.Clear();

        var items = await _db.Items.Where(x => model.Lines.Select(l => l.ItemId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        foreach (var line in model.Lines.Where(x => x.Quantity > 0 && x.ItemId > 0))
        {
            var unitCost = line.UnitCost > 0 ? line.UnitCost : items[line.ItemId].StandardCost;
            document.Lines.Add(new StockDocumentLine
            {
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                Amount = decimal.Round(line.Quantity * unitCost, 4),
                Note = line.Note?.Trim()
            });
        }

        document.TotalAmount = document.Lines.Sum(x => x.Amount);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "UPDATE", nameof(StockDocument), document.Id.ToString(),
            $"Cập nhật {UiText.DocTypeLabel(document.Type).ToLowerInvariant()} {document.DocumentNo}");

        Success($"Đã cập nhật chứng từ {document.DocumentNo}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Warehouse,Purchasing")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        await _workflow.SubmitStockDocumentAsync(id, CurrentUser);
        Success("Đã trình duyệt chứng từ.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        await _workflow.ApproveStockDocumentAsync(id, CurrentUser);
        Success("Đã phê duyệt chứng từ. Thủ kho có thể ghi sổ.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Warehouse")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(int id)
    {
        await _inventory.PostStockDocumentAsync(id, CurrentUser);
        Success("Đã ghi sổ chứng từ và cập nhật tồn kho.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Manager,Warehouse,Purchasing")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        await _workflow.CancelStockDocumentAsync(id, reason, CurrentUser);
        Success("Đã hủy chứng từ.");
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Export(StockDocumentType? type, DocumentStatus? status, string? search,
        int? warehouseId, DateOnly? from, DateOnly? to)
    {
        var query = _db.StockDocuments.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Supplier)
            .AsQueryable();

        if (type is not null) query = query.Where(x => x.Type == type);
        if (status is not null) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.DocumentNo.Contains(keyword) || (x.Note != null && x.Note.Contains(keyword)));
        }

        if (warehouseId is not null)
        {
            query = query.Where(x => x.WarehouseId == warehouseId || x.DestinationWarehouseId == warehouseId);
        }

        if (from is not null) query = query.Where(x => x.DocumentDate >= from);
        if (to is not null) query = query.Where(x => x.DocumentDate <= to);

        var rows = await query.OrderByDescending(x => x.DocumentDate).ThenByDescending(x => x.Id).Take(5000).ToListAsync();

        return Csv.Export($"chung-tu-kho-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            new[] { "Số chứng từ", "Loại", "Ngày", "Kho", "Kho nhận", "Nhà cung cấp", "Trạng thái", "Giá trị", "Ghi chú" },
            rows.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                x.DocumentNo, UiText.DocTypeLabel(x.Type), x.DocumentDate, x.Warehouse?.Name ?? "",
                x.DestinationWarehouse?.Name ?? "", x.Supplier?.Name ?? "", UiText.StatusLabel(x.Status),
                x.TotalAmount, x.Note ?? ""
            }));
    }

    private async Task ValidateDocumentAsync(StockDocumentEditViewModel model)
    {
        if (model.Type == StockDocumentType.Transfer)
        {
            if (model.DestinationWarehouseId is null)
            {
                ModelState.AddModelError(nameof(model.DestinationWarehouseId), "Phiếu điều chuyển phải chọn kho nhận.");
            }
            else if (model.DestinationWarehouseId == model.WarehouseId)
            {
                ModelState.AddModelError(nameof(model.DestinationWarehouseId), "Kho nhận phải khác kho xuất.");
            }
        }

        if (!await _db.Warehouses.AnyAsync(x => x.Id == model.WarehouseId))
        {
            ModelState.AddModelError(nameof(model.WarehouseId), "Kho không hợp lệ.");
        }

        if (model.DestinationWarehouseId is not null
            && !await _db.Warehouses.AnyAsync(x => x.Id == model.DestinationWarehouseId))
        {
            ModelState.AddModelError(nameof(model.DestinationWarehouseId), "Kho nhận không hợp lệ.");
        }

        var lines = model.Lines.Where(x => x.ItemId > 0 || x.Quantity > 0).ToList();
        if (lines.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Lines), "Chứng từ phải có ít nhất một dòng vật tư.");
            return;
        }

        if (lines.Any(x => x.ItemId <= 0 || x.Quantity <= 0))
        {
            ModelState.AddModelError(nameof(model.Lines),
                "Mỗi dòng vật tư phải chọn vật tư và có số lượng lớn hơn 0.");
        }

        var itemIds = lines.Select(x => x.ItemId).Where(x => x > 0).Distinct().ToList();
        var validItems = await _db.Items.CountAsync(x => itemIds.Contains(x.Id));
        if (validItems != itemIds.Count)
        {
            ModelState.AddModelError(nameof(model.Lines), "Có dòng vật tư không hợp lệ.");
        }
    }

    private async Task FillOptionsAsync(StockDocumentEditViewModel model)
    {
        model.WarehouseOptions = await ErpLookups.WarehouseOptionsAsync(_db, false, model.WarehouseId);
        model.SupplierOptions = await ErpLookups.SupplierOptionsAsync(_db, false, model.SupplierId);
        model.ItemOptions = await ErpLookups.ItemOptionsAsync(_db, false);
    }
}
