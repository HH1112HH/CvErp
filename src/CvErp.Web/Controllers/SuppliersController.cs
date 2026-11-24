using CvErp.Domain.Entities;
using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Services;
using CvErp.Web.ViewModels.Common;
using CvErp.Web.ViewModels.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

public class SuppliersController : BaseController
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public SuppliersController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search, bool? active, int page = 1)
    {
        var query = _db.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Code.Contains(keyword)
                                     || x.Name.Contains(keyword)
                                     || x.Phone.Contains(keyword)
                                     || x.TaxCode.Contains(keyword));
        }

        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        return View(new SupplierListViewModel
        {
            Search = search,
            Active = active,
            Suppliers = await query.OrderBy(x => x.Code).ToPagedResultAsync(page, 15)
        });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpGet]
    public IActionResult Create() => View(new SupplierEditViewModel());

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierEditViewModel model)
    {
        if (await _db.Suppliers.AnyAsync(x => x.Code == model.Code.Trim()))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã nhà cung cấp đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var supplier = new Supplier
        {
            Code = model.Code.Trim().ToUpperInvariant(),
            Name = model.Name.Trim(),
            TaxCode = model.TaxCode.Trim(),
            Phone = model.Phone.Trim(),
            Email = model.Email.Trim(),
            Address = model.Address.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "CREATE", nameof(Supplier), supplier.Id.ToString(),
            $"Tạo nhà cung cấp {supplier.Code} - {supplier.Name}");

        Success($"Đã thêm nhà cung cấp {supplier.Code}.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (supplier is null)
        {
            return NotFound();
        }

        return View(new SupplierEditViewModel
        {
            Id = supplier.Id,
            Code = supplier.Code,
            Name = supplier.Name,
            TaxCode = supplier.TaxCode,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            IsActive = supplier.IsActive
        });
    }

    [Authorize(Roles = "Administrator,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SupplierEditViewModel model)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
        if (supplier is null)
        {
            return NotFound();
        }

        if (await _db.Suppliers.AnyAsync(x => x.Code == model.Code.Trim() && x.Id != id))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã nhà cung cấp đã được dùng bởi nhà cung cấp khác.");
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        supplier.Code = model.Code.Trim().ToUpperInvariant();
        supplier.Name = model.Name.Trim();
        supplier.TaxCode = model.TaxCode.Trim();
        supplier.Phone = model.Phone.Trim();
        supplier.Email = model.Email.Trim();
        supplier.Address = model.Address.Trim();
        supplier.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUser, "UPDATE", nameof(Supplier), supplier.Id.ToString(),
            $"Cập nhật nhà cung cấp {supplier.Code} - {supplier.Name}");

        Success($"Đã cập nhật nhà cung cấp {supplier.Code}.");
        return RedirectToAction(nameof(Index));
    }
}
