using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Helpers;

public static class ErpLookups
{
    public static async Task<List<SelectListItem>> WarehouseOptionsAsync(AppDbContext db, bool activeOnly = false,
        int? selected = null)
    {
        var query = db.Warehouses.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var warehouses = await query.OrderBy(x => x.Code).ToListAsync();
        return warehouses.Select(x => new SelectListItem($"{x.Code} · {x.Name}", x.Id.ToString(),
            x.Id == selected)).ToList();
    }

    public static async Task<List<SelectListItem>> ItemOptionsAsync(AppDbContext db, bool activeOnly = false,
        ItemType? type = null, int? selected = null)
    {
        var query = db.Items.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        if (type is not null)
        {
            query = query.Where(x => x.Type == type);
        }

        var items = await query.OrderBy(x => x.Code).ToListAsync();
        return items.Select(x => new SelectListItem($"{x.Code} · {x.Name} ({x.Unit})", x.Id.ToString(),
            x.Id == selected)).ToList();
    }

    public static async Task<List<SelectListItem>> SupplierOptionsAsync(AppDbContext db, bool activeOnly = false,
        int? selected = null)
    {
        var query = db.Suppliers.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var suppliers = await query.OrderBy(x => x.Code).ToListAsync();
        return suppliers.Select(x => new SelectListItem($"{x.Code} · {x.Name}", x.Id.ToString(),
            x.Id == selected)).ToList();
    }

    public static async Task<List<SelectListItem>> ProductOptionsAsync(AppDbContext db, int? selected = null)
    {
        var products = await db.Items.AsNoTracking()
            .Where(x => x.IsActive && x.Type == ItemType.FinishedGood)
            .OrderBy(x => x.Code)
            .ToListAsync();

        return products.Select(x => new SelectListItem($"{x.Code} · {x.Name} ({x.Unit})", x.Id.ToString(),
            x.Id == selected)).ToList();
    }

    public static async Task<List<SelectListItem>> RoleOptionsAsync(string? selected = null)
    {
        await Task.CompletedTask;
        return CvErp.Domain.Constants.AppRoles.All
            .Select(x => new SelectListItem(CvErp.Web.Helpers.UiText.RoleLabel(x), x, x == selected))
            .ToList();
    }

    public static async Task<Dictionary<string, string>> UserNamesAsync(AppDbContext db, IEnumerable<string?> ids)
    {
        var list = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return await db.Users.AsNoTracking()
            .Where(x => list.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.FullName) ? x.UserName ?? x.Id : x.FullName);
    }
}
