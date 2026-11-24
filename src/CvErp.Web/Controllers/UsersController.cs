using CvErp.Domain.Constants;
using CvErp.Domain.Entities;
using CvErp.Infrastructure.Services;
using CvErp.Web.Helpers;
using CvErp.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class UsersController : BaseController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AuditService _audit;

    public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        AuditService audit)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search, string? role)
    {
        var query = _userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.UserName!.Contains(keyword)
                                     || x.FullName.Contains(keyword)
                                     || (x.Email != null && x.Email.Contains(keyword)));
        }

        var users = await query.OrderBy(x => x.UserName).ToListAsync();
        var rows = new List<UserRow>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role))
            {
                continue;
            }

            rows.Add(new UserRow { User = user, Roles = roles });
        }

        return View(new UserListViewModel
        {
            Search = search,
            Role = role,
            RoleOptions = await ErpLookups.RoleOptionsAsync(role),
            Users = rows
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(new UserCreateViewModel { RoleOptions = await ErpLookups.RoleOptionsAsync() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!AppRoles.All.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ.");
        }

        var existing = await _userManager.FindByNameAsync(model.UserName.Trim());
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.UserName), "Tên đăng nhập đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            model.RoleOptions = await ErpLookups.RoleOptionsAsync(model.Role);
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
            FullName = model.FullName.Trim(),
            EmailConfirmed = true,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
            }

            model.RoleOptions = await ErpLookups.RoleOptionsAsync(model.Role);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        await _audit.LogAsync(CurrentUser, "CREATE", nameof(ApplicationUser), user.Id,
            $"Tạo người dùng {user.UserName} với vai trò {UiText.RoleLabel(model.Role)}");

        Success($"Đã tạo người dùng {user.UserName}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var currentRole = roles.FirstOrDefault() ?? string.Empty;

        return View(new UserEditViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            FullName = user.FullName,
            Email = user.Email,
            Role = currentRole,
            CurrentRole = currentRole,
            IsActive = user.IsActive,
            RoleOptions = await ErpLookups.RoleOptionsAsync(currentRole)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserEditViewModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!AppRoles.All.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var currentRole = roles.FirstOrDefault() ?? string.Empty;
        var isSelf = user.Id == UserId;

        if (isSelf && !model.IsActive)
        {
            ModelState.AddModelError(nameof(model.IsActive), "Không thể khóa tài khoản đang đăng nhập.");
        }

        if (isSelf && model.Role != AppRoles.Administrator)
        {
            ModelState.AddModelError(nameof(model.Role), "Không thể tự bỏ quyền quản trị của chính mình.");
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.UserName = user.UserName ?? "";
            model.CurrentRole = currentRole;
            model.RoleOptions = await ErpLookups.RoleOptionsAsync(model.Role);
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                {
                    ModelState.AddModelError(nameof(model.NewPassword), TranslateIdentityError(error));
                }

                model.Id = id;
                model.UserName = user.UserName ?? "";
                model.CurrentRole = currentRole;
                model.RoleOptions = await ErpLookups.RoleOptionsAsync(model.Role);
                return View(model);
            }
        }

        user.FullName = model.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        user.IsActive = model.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
            }

            model.Id = id;
            model.UserName = user.UserName ?? "";
            model.CurrentRole = currentRole;
            model.RoleOptions = await ErpLookups.RoleOptionsAsync(model.Role);
            return View(model);
        }

        if (!string.Equals(currentRole, model.Role, StringComparison.Ordinal))
        {
            if (roles.Count > 0)
            {
                await _userManager.RemoveFromRolesAsync(user, roles);
            }

            await _userManager.AddToRoleAsync(user, model.Role);
        }

        await _audit.LogAsync(CurrentUser, "UPDATE", nameof(ApplicationUser), user.Id,
            $"Cập nhật người dùng {user.UserName}, vai trò {UiText.RoleLabel(model.Role)}, " +
            (user.IsActive ? "đang hoạt động" : "đã khóa"));

        Success($"Đã cập nhật người dùng {user.UserName}.");
        return RedirectToAction(nameof(Index));
    }

    private static string TranslateIdentityError(IdentityError error) => error.Code switch
    {
        "PasswordTooShort" => "Mật khẩu quá ngắn.",
        "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải có ký tự đặc biệt.",
        "PasswordRequiresDigit" => "Mật khẩu phải có chữ số.",
        "PasswordRequiresLower" => "Mật khẩu phải có chữ thường.",
        "PasswordRequiresUpper" => "Mật khẩu phải có chữ in hoa.",
        "DuplicateUserName" => "Tên đăng nhập đã tồn tại.",
        _ => error.Description
    };
}
