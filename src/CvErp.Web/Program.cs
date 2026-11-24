using System.Globalization;
using CvErp.Domain.Entities;
using CvErp.Infrastructure;
using CvErp.Infrastructure.Data;
using CvErp.Web.Filters;
using CvErp.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

// Toàn bộ input số/ngày dùng định dạng bất biến (dấu chấm thập phân),
// trong khi nhãn hiển thị tiếng Việt được định dạng thủ công qua UiText.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<BusinessRuleExceptionFilter>();
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var uiCulture = CultureInfo.GetCultureInfo("vi-VN");
    options.DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture, uiCulture);
    options.SupportedCultures = new List<CultureInfo> { CultureInfo.InvariantCulture };
    options.SupportedUICultures = new List<CultureInfo> { uiCulture };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    // Trang nghiệp vụ đã xác thực không được lưu cache HTTP,
    // tránh lộ dữ liệu khi dùng chung máy hoặc chế độ offline.
    var path = context.Request.Path.Value ?? string.Empty;
    var isStaticAsset = path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/icons/", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/manifest.webmanifest", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/sw.js", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/offline.html", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase);

    if (!isStaticAsset && context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
