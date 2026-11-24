using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Services;
using CvErp.Web.Helpers;
using CvErp.Web.ViewModels.Common;
using CvErp.Web.ViewModels.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

public class BomsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public BomsController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search, bool? active, int page = 1)
    {
        var query = _db.Boms.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .Include(x => x.Lines)
            .ThenInclude(x => x.ComponentItem)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Code.Contains(keyword)
                                     || x.Name.Contains(keyword)
                                     || (x.FinishedGoodItem != null && x.FinishedGoodItem.Name.Contains(keyword)));
        }

        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        return View(new BomListViewModel
        {
            Search = search,
            Active = active,
            Boms = await query.OrderByDescending(x => x.IsActive).ThenBy(x => x.Code).ToPagedResultAsync(page, 15)
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var bom = await _db.Boms.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .Include(x => x.Lines)
            .ThenInclude(x => x.ComponentItem)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (bom is null)
        {
            return NotFound();
        }

        var orderCount = await _db.ProductionOrders.AsNoTracking().CountAsync(x => x.BomId == id);
        ViewData["ProductionOrderCount"] = orderCount;
        return View(bom);
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new BomEditViewModel
        {
            Lines = new List<BomLineEditModel> { new() { QuantityPerUnit = 1 } }
        };
        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BomEditViewModel model)
    {
        var code = model.Code.Trim();
        if (await _db.Boms.AnyAsync(x => x.Code == code))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã BOM đã tồn tại.");
        }

        await ValidateLinesAsync(model);

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model);
            return View(model);
        }

        var bom = new Bom
        {
            Code = code.ToUpperInvariant(),
            Name = model.Name.Trim(),
            FinishedGoodItemId = model.FinishedGoodItemId,
            Version = model.Version.Trim(),
            IsActive = model.IsActive,
            Note = model.Note?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var line in model.Lines.Where(x => x.ComponentItemId > 0))
        {
            bom.Lines.Add(new BomLine
            {
                ComponentItemId = line.ComponentItemId,
                QuantityPerUnit = line.QuantityPerUnit,
                ScrapPercent = line.ScrapPercent,
                Note = line.Note?.Trim()
            });
        }

        _db.Boms.Add(bom);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "CREATE", nameof(Bom), bom.Id.ToString(),
            $"Tạo định mức {bom.Code} cho {model.FinishedGoodItemId} với {bom.Lines.Count} nguyên vật liệu");

        Success($"Đã tạo định mức BOM {bom.Code}.");
        return RedirectToAction(nameof(Details), new { id = bom.Id });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var bom = await _db.Boms.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (bom is null)
        {
            return NotFound();
        }

        var model = new BomEditViewModel
        {
            Id = bom.Id,
            Code = bom.Code,
            Name = bom.Name,
            FinishedGoodItemId = bom.FinishedGoodItemId,
            Version = bom.Version,
            IsActive = bom.IsActive,
            Note = bom.Note,
            Lines = bom.Lines.Select(x => new BomLineEditModel
            {
                ComponentItemId = x.ComponentItemId,
                QuantityPerUnit = x.QuantityPerUnit,
                ScrapPercent = x.ScrapPercent,
                Note = x.Note
            }).ToList()
        };

        if (model.Lines.Count == 0)
        {
            model.Lines.Add(new BomLineEditModel { QuantityPerUnit = 1 });
        }

        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BomEditViewModel model)
    {
        var bom = await _db.Boms.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (bom is null)
        {
            return NotFound();
        }

        var code = model.Code.Trim();
        if (await _db.Boms.AnyAsync(x => x.Code == code && x.Id != id))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã BOM đã được dùng bởi định mức khác.");
        }

        await ValidateLinesAsync(model);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            await FillOptionsAsync(model);
            return View(model);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.BomLines.RemoveRange(bom.Lines);
        await _db.SaveChangesAsync();

        bom.Code = code.ToUpperInvariant();
        bom.Name = model.Name.Trim();
        bom.FinishedGoodItemId = model.FinishedGoodItemId;
        bom.Version = model.Version.Trim();
        bom.IsActive = model.IsActive;
        bom.Note = model.Note?.Trim();

        foreach (var line in model.Lines.Where(x => x.ComponentItemId > 0))
        {
            _db.BomLines.Add(new BomLine
            {
                BomId = bom.Id,
                ComponentItemId = line.ComponentItemId,
                QuantityPerUnit = line.QuantityPerUnit,
                ScrapPercent = line.ScrapPercent,
                Note = line.Note?.Trim()
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        await _audit.LogAsync(CurrentUser, "UPDATE", nameof(Bom), bom.Id.ToString(),
            $"Cập nhật định mức {bom.Code}");

        Success($"Đã cập nhật định mức BOM {bom.Code}.");
        return RedirectToAction(nameof(Details), new { id = bom.Id });
    }

    private async Task ValidateLinesAsync(BomEditViewModel model)
    {
        var product = await _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.FinishedGoodItemId);
        if (product is null || product.Type != ItemType.FinishedGood)
        {
            ModelState.AddModelError(nameof(model.FinishedGoodItemId), "Thành phẩm không hợp lệ.");
        }

        var lines = model.Lines.Where(x => x.ComponentItemId > 0).ToList();
        if (lines.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Lines), "Định mức phải có ít nhất một nguyên vật liệu.");
            return;
        }

        if (lines.Any(x => x.ComponentItemId == model.FinishedGoodItemId))
        {
            ModelState.AddModelError(nameof(model.Lines), "Thành phẩm không thể là nguyên vật liệu của chính nó.");
        }

        if (lines.GroupBy(x => x.ComponentItemId).Any(g => g.Count() > 1))
        {
            ModelState.AddModelError(nameof(model.Lines), "Mỗi nguyên vật liệu chỉ được xuất hiện một lần trong định mức.");
        }

        var ids = lines.Select(x => x.ComponentItemId).Distinct().ToList();
        var validCount = await _db.Items.CountAsync(x => ids.Contains(x.Id) && x.Type == ItemType.RawMaterial);
        if (validCount != ids.Count)
        {
            ModelState.AddModelError(nameof(model.Lines), "Có nguyên vật liệu không hợp lệ hoặc không phải nguyên vật liệu.");
        }
    }

    private async Task FillOptionsAsync(BomEditViewModel model)
    {
        model.ProductOptions = await ErpLookups.ProductOptionsAsync(_db, model.FinishedGoodItemId == 0 ? null : model.FinishedGoodItemId);
        model.ComponentOptions = await ErpLookups.ItemOptionsAsync(_db, false, ItemType.RawMaterial);
    }
}
