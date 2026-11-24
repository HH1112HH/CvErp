using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Services;
using CvErp.Web.ViewModels.MasterData;
using CvErp.Web.ViewModels.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

public class ItemsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public ItemsController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search, ItemType? type, string? category, bool? active, int page = 1)
    {
        var query = _db.Items.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword));
        }

        if (type is not null)
        {
            query = query.Where(x => x.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        var model = new ItemListViewModel
        {
            Search = search,
            Type = type,
            Category = category,
            Active = active,
            Categories = await _db.Items.AsNoTracking()
                .Where(x => x.Category != "")
                .Select(x => x.Category)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(),
            Items = await query.OrderBy(x => x.Code).ToPagedResultAsync(page, 15)
        };

        return View(model);
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpGet]
    public IActionResult Create() => View(new ItemEditViewModel());

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemEditViewModel model)
    {
        if (await _db.Items.AnyAsync(x => x.Code == model.Code.Trim()))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã vật tư đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var item = new Item
        {
            Code = model.Code.Trim().ToUpperInvariant(),
            Name = model.Name.Trim(),
            Unit = model.Unit.Trim(),
            Category = model.Category.Trim(),
            Type = model.Type,
            MinStock = model.MinStock,
            StandardCost = model.StandardCost,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Items.Add(item);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "CREATE", nameof(Item), item.Id.ToString(),
            $"Tạo vật tư {item.Code} - {item.Name}");

        Success($"Đã thêm vật tư {item.Code}.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        return View(new ItemEditViewModel
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            Unit = item.Unit,
            Category = item.Category,
            Type = item.Type,
            MinStock = item.MinStock,
            StandardCost = item.StandardCost,
            IsActive = item.IsActive
        });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ItemEditViewModel model)
    {
        var item = await _db.Items.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        if (await _db.Items.AnyAsync(x => x.Code == model.Code.Trim() && x.Id != id))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã vật tư đã được dùng bởi vật tư khác.");
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        item.Code = model.Code.Trim().ToUpperInvariant();
        item.Name = model.Name.Trim();
        item.Unit = model.Unit.Trim();
        item.Category = model.Category.Trim();
        item.Type = model.Type;
        item.MinStock = model.MinStock;
        item.StandardCost = model.StandardCost;
        item.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "UPDATE", nameof(Item), item.Id.ToString(),
            $"Cập nhật vật tư {item.Code} - {item.Name}");

        Success($"Đã cập nhật vật tư {item.Code}.");
        return RedirectToAction(nameof(Index));
    }
}
