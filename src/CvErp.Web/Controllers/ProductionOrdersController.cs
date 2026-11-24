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

public class ProductionOrdersController : BaseController
{
    private readonly AppDbContext _db;
    private readonly NumberSequenceService _numbers;
    private readonly WorkflowService _workflow;
    private readonly ProductionService _production;
    private readonly AuditService _audit;

    public ProductionOrdersController(AppDbContext db, NumberSequenceService numbers, WorkflowService workflow,
        ProductionService production, AuditService audit)
    {
        _db = db;
        _numbers = numbers;
        _workflow = workflow;
        _production = production;
        _audit = audit;
    }

    public async Task<IActionResult> Index(ProductionOrderStatus? status, string? search, DateOnly? from, DateOnly? to,
        int page = 1)
    {
        var query = _db.ProductionOrders.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.OrderNo.Contains(keyword)
                                     || (x.FinishedGoodItem != null && x.FinishedGoodItem.Name.Contains(keyword)));
        }

        if (from is not null)
        {
            query = query.Where(x => x.DueDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(x => x.DueDate <= to);
        }

        return View(new ProductionOrderListViewModel
        {
            Status = status,
            Search = search,
            From = from,
            To = to,
            Orders = await query.OrderByDescending(x => x.DueDate).ThenByDescending(x => x.Id)
                .ToPagedResultAsync(page, 15)
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.ProductionOrders.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .Include(x => x.Bom)
            .Include(x => x.OutputWarehouse)
            .Include(x => x.Materials).ThenInclude(x => x.Item)
            .Include(x => x.MaterialIssues).ThenInclude(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.MaterialIssues).ThenInclude(x => x.Warehouse)
            .Include(x => x.Outputs)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        var postedLines = order.MaterialIssues
            .Where(x => x.Status == DocumentStatus.Posted)
            .SelectMany(x => x.Lines)
            .ToList();

        var requirements = order.Materials.Select(material =>
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
        }).ToList();

        var users = await ErpLookups.UserNamesAsync(_db, new[]
        {
            order.CreatedById, order.SubmittedById, order.ApprovedById, order.CancelledById
        });

        return View(new ProductionOrderDetailsViewModel
        {
            Order = order,
            Requirements = requirements,
            UserNames = users,
            MaterialIssues = order.MaterialIssues.OrderByDescending(x => x.IssueDate).ToList(),
            StandardCost = order.Materials.Sum(x => x.StandardAmount),
            ActualCost = postedLines.Sum(x => x.Amount),
            IssuedStandardQuantity = order.Materials.Sum(x => x.StandardQuantity),
            IssuedActualQuantity = postedLines.Sum(x => x.Quantity)
        });
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ProductionOrderCreateViewModel
        {
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today).AddDays(14)
        };
        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductionOrderCreateViewModel model)
    {
        var bom = await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model);
            return View(model);
        }

        var order = new ProductionOrder
        {
            OrderNo = await _numbers.NextAsync(DocumentPrefixes.ProductionOrder, model.StartDate),
            FinishedGoodItemId = model.FinishedGoodItemId,
            BomId = bom!.Id,
            OutputWarehouseId = model.OutputWarehouseId,
            PlannedQuantity = model.PlannedQuantity,
            StartDate = model.StartDate,
            DueDate = model.DueDate,
            Status = ProductionOrderStatus.Draft,
            Note = model.Note?.Trim(),
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow
        };

        ApplyMaterialSnapshot(order, bom, model.PlannedQuantity);
        _db.ProductionOrders.Add(order);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "CREATE", nameof(ProductionOrder), order.Id.ToString(),
            $"Tạo lệnh sản xuất {order.OrderNo} cho {UiText.Qty(model.PlannedQuantity)} sản phẩm");

        Success($"Đã tạo lệnh sản xuất {order.OrderNo} ở trạng thái Nháp.");
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await _db.ProductionOrders.AsNoTracking()
            .Include(x => x.Materials)
            .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        if (order.Status != ProductionOrderStatus.Draft)
        {
            Error("Chỉ lệnh sản xuất nháp mới được chỉnh sửa.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new ProductionOrderCreateViewModel
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            FinishedGoodItemId = order.FinishedGoodItemId,
            OutputWarehouseId = order.OutputWarehouseId,
            PlannedQuantity = order.PlannedQuantity,
            StartDate = order.StartDate,
            DueDate = order.DueDate,
            Note = order.Note
        };

        model.ExistingMaterials = order.Materials.OrderBy(x => x.Item?.Code).ToList();
        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductionOrderCreateViewModel model)
    {
        var order = await _db.ProductionOrders.Include(x => x.Materials).FirstOrDefaultAsync(x => x.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status != ProductionOrderStatus.Draft)
        {
            Error("Chỉ lệnh sản xuất nháp mới được chỉnh sửa.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var bom = await ValidateAsync(model);
        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.OrderNo = order.OrderNo;
            await FillOptionsAsync(model);
            return View(model);
        }

        order.FinishedGoodItemId = model.FinishedGoodItemId;
        order.BomId = bom!.Id;
        order.OutputWarehouseId = model.OutputWarehouseId;
        order.PlannedQuantity = model.PlannedQuantity;
        order.StartDate = model.StartDate;
        order.DueDate = model.DueDate;
        order.Note = model.Note?.Trim();

        _db.ProductionOrderMaterials.RemoveRange(order.Materials);
        order.Materials.Clear();
        order.StandardMaterialCost = 0;
        ApplyMaterialSnapshot(order, bom, model.PlannedQuantity);

        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "UPDATE", nameof(ProductionOrder), order.Id.ToString(),
            $"Cập nhật lệnh sản xuất {order.OrderNo}");

        Success($"Đã cập nhật lệnh sản xuất {order.OrderNo}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        await _workflow.SubmitProductionOrderAsync(id, CurrentUser);
        Success("Đã trình duyệt lệnh sản xuất.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        await _workflow.ApproveProductionOrderAsync(id, CurrentUser);
        Success("Đã phê duyệt lệnh sản xuất. Bộ phận sản xuất có thể cấp phát vật tư.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Manager,Production")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        await _workflow.CancelProductionOrderAsync(id, reason, CurrentUser);
        Success("Đã hủy lệnh sản xuất.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpGet]
    public async Task<IActionResult> Complete(int id)
    {
        var order = await _db.ProductionOrders.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        if (order.Status is not (ProductionOrderStatus.Approved or ProductionOrderStatus.InProgress))
        {
            Error("Chỉ lệnh đã duyệt hoặc đang sản xuất mới được ghi nhận hoàn thành.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(new CompleteProductionOrderViewModel
        {
            ProductionOrderId = order.Id,
            OrderNo = order.OrderNo,
            ProductName = order.FinishedGoodItem?.Name ?? "",
            PlannedQuantity = order.PlannedQuantity,
            GoodQuantity = order.PlannedQuantity,
            DefectQuantity = 0
        });
    }

    [Authorize(Roles = "Administrator,Production")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, CompleteProductionOrderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ProductionOrderId = id;
            return View(model);
        }

        await _production.CompleteProductionOrderAsync(id, model.GoodQuantity, model.DefectQuantity,
            model.Note, CurrentUser);

        Success("Đã ghi nhận hoàn thành lệnh sản xuất và nhập thành phẩm vào kho.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> BomPreview(int productId, decimal quantity)
    {
        quantity = quantity <= 0 ? 1 : quantity;
        var bom = await _db.Boms.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.ComponentItem)
            .Where(x => x.FinishedGoodItemId == productId && x.IsActive)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (bom is null)
        {
            return Json(new { found = false, bomCode = (string?)null, rows = Array.Empty<object>() });
        }

        var rows = bom.Lines
            .OrderBy(x => x.ComponentItem?.Code)
            .Select(line =>
            {
                var netQuantity = quantity * line.QuantityPerUnit;
                var standardQuantity = decimal.Round(netQuantity * (1 + line.ScrapPercent / 100m), 4,
                    MidpointRounding.AwayFromZero);
                var unitCost = line.ComponentItem?.StandardCost ?? 0;
                return new
                {
                    code = line.ComponentItem?.Code ?? "",
                    name = line.ComponentItem?.Name ?? "",
                    unit = line.ComponentItem?.Unit ?? "",
                    standardQuantity,
                    unitCost,
                    amount = decimal.Round(standardQuantity * unitCost, 4)
                };
            })
            .ToList();

        return Json(new { found = true, bomCode = bom.Code, bomName = bom.Name, rows });
    }

    private async Task<Bom?> ValidateAsync(ProductionOrderCreateViewModel model)
    {
        var product = await _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.FinishedGoodItemId);
        if (product is null || product.Type != ItemType.FinishedGood)
        {
            ModelState.AddModelError(nameof(model.FinishedGoodItemId), "Thành phẩm không hợp lệ.");
        }

        if (!await _db.Warehouses.AnyAsync(x => x.Id == model.OutputWarehouseId))
        {
            ModelState.AddModelError(nameof(model.OutputWarehouseId), "Kho nhập thành phẩm không hợp lệ.");
        }

        if (model.DueDate < model.StartDate)
        {
            ModelState.AddModelError(nameof(model.DueDate), "Ngày hoàn thành phải sau hoặc bằng ngày bắt đầu.");
        }

        var bom = await _db.Boms.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.ComponentItem)
            .Where(x => x.FinishedGoodItemId == model.FinishedGoodItemId && x.IsActive)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (bom is null || bom.Lines.Count == 0)
        {
            ModelState.AddModelError(nameof(model.FinishedGoodItemId),
                "Thành phẩm chưa có định mức BOM đang áp dụng. Hãy tạo BOM trước.");
        }

        return bom;
    }

    private static void ApplyMaterialSnapshot(ProductionOrder order, Bom bom, decimal plannedQuantity)
    {
        foreach (var line in bom.Lines)
        {
            var standardQuantity = decimal.Round(
                plannedQuantity * line.QuantityPerUnit * (1 + line.ScrapPercent / 100m), 4,
                MidpointRounding.AwayFromZero);
            var unitCost = line.ComponentItem?.StandardCost ?? 0;

            order.Materials.Add(new ProductionOrderMaterial
            {
                ItemId = line.ComponentItemId,
                StandardQuantity = standardQuantity,
                UnitCost = unitCost,
                StandardAmount = decimal.Round(standardQuantity * unitCost, 4)
            });

            order.StandardMaterialCost += decimal.Round(standardQuantity * unitCost, 4);
        }
    }

    private async Task FillOptionsAsync(ProductionOrderCreateViewModel model)
    {
        model.ProductOptions = await ErpLookups.ProductOptionsAsync(_db,
            model.FinishedGoodItemId == 0 ? null : model.FinishedGoodItemId);
        model.WarehouseOptions = await ErpLookups.WarehouseOptionsAsync(_db, true,
            model.OutputWarehouseId == 0 ? null : model.OutputWarehouseId);
    }
}
