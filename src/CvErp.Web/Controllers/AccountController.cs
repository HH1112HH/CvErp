using CvErp.Domain.Entities;
using CvErp.Infrastructure.Common;
using CvErp.Infrastructure.Services;
using CvErp.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CvErp.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditService _audit;

    public AccountController(SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager, AuditService audit)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _audit = audit;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByNameAsync(model.Username.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            await LogAuditAsync(model.Username, "LOGIN_FAILED", "Đăng nhập thất bại");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đã bị khóa. Liên hệ quản trị viên.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _audit.LogAsync(new UserContext(user.Id, user.UserName!, HttpContext.Connection.RemoteIpAddress?.ToString()),
                "LOGIN", "Account", user.Id, "Đăng nhập hệ thống");

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm khóa 5 phút do nhập sai quá nhiều lần.");
            await LogAuditAsync(user.UserName ?? model.Username, "LOGIN_FAILED", "Tài khoản bị tạm khóa");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            await LogAuditAsync(user.UserName ?? model.Username, "LOGIN_FAILED", "Sai mật khẩu");
        }

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task LogAuditAsync(string username, string action, string description)
    {
        try
        {
            await _audit.LogAsync(new UserContext(string.Empty, username,
                HttpContext.Connection.RemoteIpAddress?.ToString()), action, "Account", null, description);
        }
        catch
        {
            // Không để lỗi ghi log ảnh hưởng đến quá trình đăng nhập.
        }
    }
}
