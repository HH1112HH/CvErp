using CvErp.Infrastructure.Services;
using CvErp.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CvErp.Web.Controllers;

public class DashboardController : BaseController
{
    private readonly ReportingService _reporting;

    public DashboardController(ReportingService reporting)
    {
        _reporting = reporting;
    }

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var inventory = await _reporting.GetInventoryOverviewAsync(start, end);
        var production = await _reporting.GetProductionOverviewAsync(start, end);
        var variance = await _reporting.GetVarianceOverviewAsync(start, end);

        return View(new OverallDashboardViewModel
        {
            From = start,
            To = end,
            Inventory = inventory,
            Production = production,
            Variance = IsAdmin || IsManager ? variance : new()
        });
    }

    public async Task<IActionResult> Inventory(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var data = await _reporting.GetInventoryOverviewAsync(start, end);
        return View(new InventoryDashboardViewModel { From = start, To = end, Data = data });
    }

    public async Task<IActionResult> Production(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var data = await _reporting.GetProductionOverviewAsync(start, end);
        return View(new ProductionDashboardViewModel { From = start, To = end, Data = data });
    }

    [Authorize(Roles = "Administrator,Manager")]
    public async Task<IActionResult> Variance(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var data = await _reporting.GetVarianceOverviewAsync(start, end);
        return View(new VarianceDashboardViewModel { From = start, To = end, Data = data });
    }
}
