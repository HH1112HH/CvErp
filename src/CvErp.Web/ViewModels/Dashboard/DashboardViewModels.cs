using CvErp.Infrastructure.Reporting;

namespace CvErp.Web.ViewModels.Dashboard;

public class DashboardFilter
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

public class OverallDashboardViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public InventoryOverview Inventory { get; set; } = new();
    public ProductionOverview Production { get; set; } = new();
    public VarianceOverview Variance { get; set; } = new();
}

public class InventoryDashboardViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public InventoryOverview Data { get; set; } = new();
}

public class ProductionDashboardViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public ProductionOverview Data { get; set; } = new();
}

public class VarianceDashboardViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public VarianceOverview Data { get; set; } = new();
}
