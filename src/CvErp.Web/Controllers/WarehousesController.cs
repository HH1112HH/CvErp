using CvErp.Domain.Entities;
using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Services;
using CvErp.Web.ViewModels.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

public class WarehousesController : BaseController
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public WarehousesController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search, bool? active)
    {
        var query = _db.Warehouses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword) || x.Location.Contains(keyword));
        }

        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        var warehouses = await query.OrderBy(x => x.Code).ToListAsync();
        var balances = await _db.InventoryTransactions.AsNoTracking()
            .GroupBy(x => new { x.WarehouseId, x.ItemId })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.ItemId,
                Quantity = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        return View(new WarehouseListViewModel
        {
            Search = search,
            Active = active,
            Warehouses = warehouses,
            StockValues = balances.GroupBy(x => x.WarehouseId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount)),
            ItemCounts = balances.Where(x => x.Quantity != 0)
                .GroupBy(x => x.WarehouseId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ItemId).Distinct().Count())
        });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpGet]
    public IActionResult Create() => View(new WarehouseEditViewModel());

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WarehouseEditViewModel model)
    {
        if (await _db.Warehouses.AnyAsync(x => x.Code == model.Code.Trim()))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã kho đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var warehouse = new Warehouse
        {
            Code = model.Code.Trim().ToUpperInvariant(),
            Name = model.Name.Trim(),
            Location = model.Location.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "CREATE", nameof(Warehouse), warehouse.Id.ToString(),
            $"Tạo kho {warehouse.Code} - {warehouse.Name}");

        Success($"Đã thêm kho {warehouse.Code}.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var warehouse = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (warehouse is null)
        {
            return NotFound();
        }

        return View(new WarehouseEditViewModel
        {
            Id = warehouse.Id,
            Code = warehouse.Code,
            Name = warehouse.Name,
            Location = warehouse.Location,
            IsActive = warehouse.IsActive
        });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, WarehouseEditViewModel model)
    {
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == id);
        if (warehouse is null)
        {
            return NotFound();
        }

        if (await _db.Warehouses.AnyAsync(x => x.Code == model.Code.Trim() && x.Id != id))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã kho đã được dùng bởi kho khác.");
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        warehouse.Code = model.Code.Trim().ToUpperInvariant();
        warehouse.Name = model.Name.Trim();
        warehouse.Location = model.Location.Trim();
        warehouse.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "UPDATE", nameof(Warehouse), warehouse.Id.ToString(),
            $"Cập nhật kho {warehouse.Code} - {warehouse.Name}");

        Success($"Đã cập nhật kho {warehouse.Code}.");
        return RedirectToAction(nameof(Index));
    }
}
