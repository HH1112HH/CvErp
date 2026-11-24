using System.ComponentModel.DataAnnotations;
using CvErp.Domain.Entities;
using CvErp.Web.ViewModels.Common;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CvErp.Web.ViewModels.Admin;

public class AuditLogListViewModel
{
    public string? Search { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? UserName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public List<string> Actions { get; set; } = new();
    public List<string> EntityNames { get; set; } = new();
    public List<string> UserNames { get; set; } = new();
    public PagedResult<AuditLog> Logs { get; set; } = PagedResult<AuditLog>.Empty();
}

public class UserRow
{
    public ApplicationUser User { get; set; } = new();
    public IList<string> Roles { get; set; } = new List<string>();
}

public class UserListViewModel
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public List<SelectListItem> RoleOptions { get; set; } = new();
    public List<UserRow> Users { get; set; } = new();
}

public class UserCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự.")]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Tên đăng nhập chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public List<SelectListItem> RoleOptions { get; set; } = new();
}

public class UserEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
    [Display(Name = "Mật khẩu mới")]
    public string? NewPassword { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public string CurrentRole { get; set; } = string.Empty;
    public List<SelectListItem> RoleOptions { get; set; } = new();
}
