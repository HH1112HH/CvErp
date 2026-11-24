using System.Security.Claims;
using CvErp.Domain.Constants;
using CvErp.Infrastructure.Common;
using Microsoft.AspNetCore.Mvc;

namespace CvErp.Web.Controllers;

[Microsoft.AspNetCore.Authorization.Authorize]
public abstract class BaseController : Controller
{
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    protected string UserName => User.Identity?.Name ?? "system";

    protected UserContext CurrentUser => new(
        UserId,
        UserName,
        HttpContext.Connection.RemoteIpAddress?.ToString());

    protected bool IsAdmin => User.IsInRole(AppRoles.Administrator);
    protected bool IsManager => User.IsInRole(AppRoles.Manager);
    protected bool CanApprove => IsAdmin || IsManager;
    protected bool CanStock => IsAdmin || User.IsInRole(AppRoles.Warehouse);
    protected bool CanProduce => IsAdmin || User.IsInRole(AppRoles.Production);
    protected bool CanPurchase => IsAdmin || IsManager || User.IsInRole(AppRoles.Purchasing);
    protected bool CanManageMasterData => IsAdmin || IsManager;

    protected void Success(string message) => TempData["Success"] = message;
    protected void Error(string message) => TempData["Error"] = message;

    protected string RoleDisplayName
    {
        get
        {
            if (IsAdmin) return "Quản trị hệ thống";
            if (IsManager) return "Quản lý";
            if (User.IsInRole(AppRoles.Warehouse)) return "Thủ kho";
            if (User.IsInRole(AppRoles.Production)) return "Sản xuất";
            if (User.IsInRole(AppRoles.Purchasing)) return "Mua hàng";
            return "Người dùng";
        }
    }

    protected IActionResult LocalRedirectOr(string? url, string fallbackAction = "Index", string? controller = null)
    {
        if (!string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url))
        {
            return Redirect(url);
        }

        return controller is null ? RedirectToAction(fallbackAction) : RedirectToAction(fallbackAction, controller);
    }

    /// <summary>Khoảng ngày mặc định: 6 tháng gần nhất tính đến hôm nay.</summary>
    protected static (DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to, int defaultMonths = 6)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1).AddMonths(-(defaultMonths - 1));
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }
}
