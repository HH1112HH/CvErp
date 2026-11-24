using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Common;
using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Services;
using CvErp.Web.Helpers;
using CvErp.Web.ViewModels.Common;
using CvErp.Web.ViewModels.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

public class MaterialIssuesController : BaseController
{
    private readonly AppDbContext _db;
    private readonly NumberSequenceService _numbers;
    private readonly WorkflowService _workflow;
    private readonly InventoryService _inventory;
    private readonly AuditService _audit;

    public MaterialIssuesController(AppDbContext db, NumberSequenceService numbers, WorkflowService workflow,
        InventoryService inventory, AuditService audit)
    {
        _db = db;
        _numbers = numbers;
        _workflow = workflow;
        _inventory = inventory;
        _audit = audit;
    }

    public async Task<IActionResult> Index(DocumentStatus? status, string? search, DateOnly? from, DateOnly? to,
        int? productionOrderId, int page = 1)
    {
        var query = _db.MaterialIssues.AsNoTracking()
            .Include(x => x.ProductionOrder).ThenInclude(x => x!.FinishedGoodItem)
            .Include(x => x.Warehouse)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.IssueNo.Contains(keyword)
                                     || (x.ProductionOrder != null && x.ProductionOrder.OrderNo.Contains(keyword)));
        }

        if (from is not null)
        {
            query = query.Where(x => x.IssueDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(x => x.IssueDate <= to);
        }

        if (productionOrderId is not null)
        {
            query = query.Where(x => x.ProductionOrderId == productionOrderId);
        }

        return View(new MaterialIssueListViewModel
        {
            Status = status,
            Search = search,
            From = from,
            To = to,
            ProductionOrderId = productionOrderId,
            Issues = await query.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.Id)
                .ToPagedResultAsync(page, 15)
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var issue = await _db.MaterialIssues.AsNoTracking()
            .Include(x => x.ProductionOrder).ThenInclude(x => x!.FinishedGoodItem)
            .Include(x => x.ProductionOrder).ThenInclude(x => x!.Materials).ThenInclude(x => x.Item)
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (issue is null)
        {
            return NotFound();
        }

        var users = await ErpLookups.UserNamesAsync(_db, new[]
        {
            issue.CreatedById, issue.SubmittedById, issue.ApprovedById, issue.PostedById, issue.CancelledById
        });

        var postedLines = await _db.MaterialIssues.AsNoTracking()
            .Where(x => x.ProductionOrderId == issue.ProductionOrderId && x.Status == DocumentStatus.Posted)
            .SelectMany(x => x.Lines)
            .ToListAsync();

        var requirements = issue.ProductionOrder?.Materials.Select(material =>
        {
            var issued = postedLines.Where(x => x.ItemId == material.ItemId).ToList();
            return new MaterialRequirementRow
            {
                Code = material.Item?.Code ?? "",
                Name = material.Item?.Name ?? "",
                Unit = material.Item?.Unit ?? "",
                StandardQuantity = material.StandardQuantity,
                IssuedQuantity = issued.Sum(x => x.Quantity),
                StandardAmount = material.StandardAmount,
                IssuedAmount = issued.Sum(x => x.Amount)
            };
        }).ToList() ?? new List<MaterialRequirementRow>();

        return View(new MaterialIssueDetailsViewModel
        {
            Issue = issue,
            UserNames = users,
            Requirements = requirements
        });
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpGet]
    public async Task<IActionResult> Create(int? orderId)
    {
        var model = new MaterialIssueEditViewModel
        {
            IssueDate = DateOnly.FromDateTime(DateTime.Today),
            WarehouseId = await _db.Warehouses.Where(x => x.Code == "KHO-NVL").Select(x => x.Id).FirstOrDefaultAsync()
        };

        if (model.WarehouseId == 0)
        {
            var firstWarehouse = await _db.Warehouses.OrderBy(x => x.Code).FirstOrDefaultAsync();
            model.WarehouseId = firstWarehouse?.Id ?? 0;
        }

        ProductionOrder? order = null;
        if (orderId is not null)
        {
            order = await _db.ProductionOrders.AsNoTracking()
                .Include(x => x.FinishedGoodItem)
                .Include(x => x.Materials).ThenInclude(x => x.Item)
                .Include(x => x.MaterialIssues).ThenInclude(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == orderId);
        }
        else
        {
            order = await _db.ProductionOrders.AsNoTracking()
                .Include(x => x.FinishedGoodItem)
                .Include(x => x.Materials).ThenInclude(x => x.Item)
                .Include(x => x.MaterialIssues).ThenInclude(x => x.Lines)
                .Where(x => x.Status == ProductionOrderStatus.Approved || x.Status == ProductionOrderStatus.InProgress)
                .OrderBy(x => x.DueDate)
                .FirstOrDefaultAsync();
        }

        if (order is not null && order.Status is ProductionOrderStatus.Approved or ProductionOrderStatus.InProgress)
        {
            model.ProductionOrderId = order.Id;
            model.ReturnOrderId = order.Id;

            var postedLines = order.MaterialIssues
                .Where(x => x.Status == DocumentStatus.Posted)
                .SelectMany(x => x.Lines)
                .ToList();

            foreach (var material in order.Materials)
            {
                var issued = postedLines.Where(x => x.ItemId == material.ItemId).Sum(x => x.Quantity);
                var remaining = material.StandardQuantity - issued;
                if (remaining <= 0)
                {
                    continue;
                }

                model.Lines.Add(new MaterialIssueLineEditModel
                {
                    ItemId = material.ItemId,
                    Quantity = decimal.Round(remaining, 4, MidpointRounding.AwayFromZero),
                    UnitCost = material.UnitCost
                });
            }
        }

        if (model.Lines.Count == 0)
        {
            model.Lines.Add(new MaterialIssueLineEditModel { Quantity = 1 });
        }

        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MaterialIssueEditViewModel model)
    {
        model.Lines ??= new List<MaterialIssueLineEditModel>();
        var order = await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model);
            return View(model);
        }

        var issue = new MaterialIssue
        {
            IssueNo = await _numbers.NextAsync(DocumentPrefixes.MaterialIssue, model.IssueDate),
            ProductionOrderId = model.ProductionOrderId,
            WarehouseId = model.WarehouseId,
            IssueDate = model.IssueDate,
            Status = DocumentStatus.Draft,
            Note = model.Note?.Trim(),
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow
        };

        var items = await _db.Items.Where(x => model.Lines.Select(l => l.ItemId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        foreach (var line in model.Lines.Where(x => x.ItemId > 0 && x.Quantity > 0))
        {
            var unitCost = line.UnitCost > 0 ? line.UnitCost : items[line.ItemId].StandardCost;
            issue.Lines.Add(new MaterialIssueLine
            {
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                Amount = decimal.Round(line.Quantity * unitCost, 4),
                Note = line.Note?.Trim()
            });
        }

        issue.TotalAmount = issue.Lines.Sum(x => x.Amount);
        _db.MaterialIssues.Add(issue);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "CREATE", nameof(MaterialIssue), issue.Id.ToString(),
            $"Tạo phiếu cấp phát {issue.IssueNo} cho lệnh {order!.OrderNo}");

        Success($"Đã tạo phiếu cấp phát {issue.IssueNo} ở trạng thái Nháp.");
        return RedirectToAction(nameof(Details), new { id = issue.Id });
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var issue = await _db.MaterialIssues.AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.ProductionOrder).ThenInclude(x => x!.FinishedGoodItem)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (issue is null)
        {
            return NotFound();
        }

        if (issue.Status != DocumentStatus.Draft)
        {
            Error("Chỉ phiếu cấp phát nháp mới được chỉnh sửa.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new MaterialIssueEditViewModel
        {
            Id = issue.Id,
            IssueNo = issue.IssueNo,
            ProductionOrderId = issue.ProductionOrderId,
            WarehouseId = issue.WarehouseId,
            IssueDate = issue.IssueDate,
            Note = issue.Note,
            ReturnOrderId = issue.ProductionOrderId,
            Lines = issue.Lines.Select(x => new MaterialIssueLineEditModel
            {
                ItemId = x.ItemId,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost,
                Note = x.Note
            }).ToList()
        };

        if (model.Lines.Count == 0)
        {
            model.Lines.Add(new MaterialIssueLineEditModel { Quantity = 1 });
        }

        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MaterialIssueEditViewModel model)
    {
        var issue = await _db.MaterialIssues.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (issue is null)
        {
            return NotFound();
        }

        if (issue.Status != DocumentStatus.Draft)
        {
            Error("Chỉ phiếu cấp phát nháp mới được chỉnh sửa.");
            return RedirectToAction(nameof(Details), new { id });
        }

        model.ProductionOrderId = issue.ProductionOrderId;
        model.Lines ??= new List<MaterialIssueLineEditModel>();
        var order = await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.IssueNo = issue.IssueNo;
            await FillOptionsAsync(model);
            return View(model);
        }

        issue.WarehouseId = model.WarehouseId;
        issue.IssueDate = model.IssueDate;
        issue.Note = model.Note?.Trim();

        _db.MaterialIssueLines.RemoveRange(issue.Lines);
        issue.Lines.Clear();

        var items = await _db.Items.Where(x => model.Lines.Select(l => l.ItemId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        foreach (var line in model.Lines.Where(x => x.ItemId > 0 && x.Quantity > 0))
        {
            var unitCost = line.UnitCost > 0 ? line.UnitCost : items[line.ItemId].StandardCost;
            issue.Lines.Add(new MaterialIssueLine
            {
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                Amount = decimal.Round(line.Quantity * unitCost, 4),
                Note = line.Note?.Trim()
            });
        }

        issue.TotalAmount = issue.Lines.Sum(x => x.Amount);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "UPDATE", nameof(MaterialIssue), issue.Id.ToString(),
            $"Cập nhật phiếu cấp phát {issue.IssueNo}");

        Success($"Đã cập nhật phiếu cấp phát {issue.IssueNo}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        await _workflow.SubmitMaterialIssueAsync(id, CurrentUser);
        Success("Đã trình duyệt phiếu cấp phát.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        await _workflow.ApproveMaterialIssueAsync(id, CurrentUser);
        Success("Đã phê duyệt phiếu cấp phát. Thủ kho có thể ghi sổ xuất kho.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Warehouse")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(int id)
    {
        await _inventory.PostMaterialIssueAsync(id, CurrentUser);
        Success("Đã ghi sổ phiếu cấp phát và trừ tồn kho nguyên vật liệu.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Manager,Production,Warehouse")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        await _workflow.CancelMaterialIssueAsync(id, reason, CurrentUser);
        Success("Đã hủy phiếu cấp phát.");
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Export(DocumentStatus? status, string? search, DateOnly? from, DateOnly? to)
    {
        var query = _db.MaterialIssues.AsNoTracking()
            .Include(x => x.ProductionOrder)
            .Include(x => x.Warehouse)
            .AsQueryable();

        if (status is not null) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.IssueNo.Contains(keyword)
                                     || (x.ProductionOrder != null && x.ProductionOrder.OrderNo.Contains(keyword)));
        }

        if (from is not null) query = query.Where(x => x.IssueDate >= from);
        if (to is not null) query = query.Where(x => x.IssueDate <= to);

        var rows = await query.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.Id).Take(5000).ToListAsync();

        return Csv.Export($"cap-phat-vat-tu-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            new[] { "Số phiếu", "Ngày", "Lệnh sản xuất", "Kho xuất", "Trạng thái", "Tổng giá trị", "Ghi chú" },
            rows.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                x.IssueNo, x.IssueDate, x.ProductionOrder?.OrderNo ?? "", x.Warehouse?.Name ?? "",
                UiText.StatusLabel(x.Status), x.TotalAmount, x.Note ?? ""
            }));
    }

    private async Task<ProductionOrder?> ValidateAsync(MaterialIssueEditViewModel model)
    {
        var order = await _db.ProductionOrders.AsNoTracking()
            .Include(x => x.Materials)
            .FirstOrDefaultAsync(x => x.Id == model.ProductionOrderId);

        if (order is null)
        {
            ModelState.AddModelError(nameof(model.ProductionOrderId), "Lệnh sản xuất không hợp lệ.");
        }
        else if (order.Status is not (ProductionOrderStatus.Approved or ProductionOrderStatus.InProgress))
        {
            ModelState.AddModelError(nameof(model.ProductionOrderId),
                "Chỉ cấp phát cho lệnh đã duyệt hoặc đang sản xuất.");
        }

        if (!await _db.Warehouses.AnyAsync(x => x.Id == model.WarehouseId))
        {
            ModelState.AddModelError(nameof(model.WarehouseId), "Kho xuất không hợp lệ.");
        }

        var lines = model.Lines.Where(x => x.ItemId > 0 || x.Quantity > 0).ToList();
        if (lines.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Lines), "Phiếu cấp phát phải có ít nhất một dòng vật tư.");
            return order;
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

        if (order is not null)
        {
            var bomItemIds = order.Materials.Select(x => x.ItemId).ToHashSet();
            if (lines.Any(x => x.ItemId > 0 && !bomItemIds.Contains(x.ItemId)))
            {
                ModelState.AddModelError(nameof(model.Lines),
                    "Có vật tư không nằm trong nhu cầu của lệnh sản xuất. Hãy kiểm tra lại định mức.");
            }
        }

        return order;
    }

    private async Task FillOptionsAsync(MaterialIssueEditViewModel model)
    {
        var orders = await _db.ProductionOrders.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .Where(x => x.Status == ProductionOrderStatus.Approved || x.Status == ProductionOrderStatus.InProgress)
            .OrderBy(x => x.DueDate)
            .ToListAsync();

        model.OrderOptions = orders.Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
            $"{x.OrderNo} · {x.FinishedGoodItem?.Name} ({UiText.Qty(x.PlannedQuantity)} {x.FinishedGoodItem?.Unit})",
            x.Id.ToString(), x.Id == model.ProductionOrderId)).ToList();

        model.WarehouseOptions = await ErpLookups.WarehouseOptionsAsync(_db, true, model.WarehouseId);
        model.ItemOptions = await ErpLookups.ItemOptionsAsync(_db, true, ItemType.RawMaterial);
    }
}
