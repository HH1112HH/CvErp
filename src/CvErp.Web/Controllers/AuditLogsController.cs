using CvErp.Domain.Entities;
using CvErp.Infrastructure.Data;
using CvErp.Web.Helpers;
using CvErp.Web.ViewModels.Admin;
using CvErp.Web.ViewModels.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

[Authorize(Roles = "Administrator,Manager")]
public class AuditLogsController : BaseController
{
    private readonly AppDbContext _db;

    public AuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, string? actionFilter, string? entityName, string? userName,
        DateOnly? from, DateOnly? to, int page = 1)
    {
        var query = BuildQuery(search, actionFilter, entityName, userName, from, to);

        var model = new AuditLogListViewModel
        {
            Search = search,
            Action = actionFilter,
            EntityName = entityName,
            UserName = userName,
            From = from,
            To = to,
            Actions = await _db.AuditLogs.AsNoTracking().Select(x => x.Action).Distinct().OrderBy(x => x).ToListAsync(),
            EntityNames = await _db.AuditLogs.AsNoTracking().Select(x => x.EntityName).Distinct().OrderBy(x => x).ToListAsync(),
            UserNames = await _db.AuditLogs.AsNoTracking().Select(x => x.UserName).Distinct().OrderBy(x => x).ToListAsync(),
            Logs = await query.OrderByDescending(x => x.Timestamp).ToPagedResultAsync(page, 25)
        };

        return View(model);
    }

    public async Task<IActionResult> Export(string? search, string? actionFilter, string? entityName, string? userName,
        DateOnly? from, DateOnly? to)
    {
        var rows = await BuildQuery(search, actionFilter, entityName, userName, from, to)
            .OrderByDescending(x => x.Timestamp)
            .Take(10000)
            .ToListAsync();

        return Csv.Export($"nhat-ky-thao-tac-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            new[] { "Thời gian", "Người dùng", "Hành động", "Đối tượng", "Mã đối tượng", "Mô tả", "IP" },
            rows.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                x.Timestamp, x.UserName, UiText.ActionLabel(x.Action), x.EntityName, x.EntityId ?? "",
                x.Description ?? "", x.IpAddress ?? ""
            }));
    }

    private IQueryable<AuditLog> BuildQuery(string? search, string? actionFilter, string? entityName, string? userName,
        DateOnly? from, DateOnly? to)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Description!.Contains(keyword)
                                     || x.EntityId!.Contains(keyword)
                                     || x.UserName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(x => x.Action == actionFilter);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(x => x.EntityName == entityName);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(x => x.UserName == userName);
        }

        if (from is not null)
        {
            var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(x => x.Timestamp >= fromUtc);
        }

        if (to is not null)
        {
            var toUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(x => x.Timestamp < toUtc);
        }

        return query;
    }
}
