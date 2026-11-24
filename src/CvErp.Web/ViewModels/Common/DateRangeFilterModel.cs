namespace CvErp.Web.ViewModels.Common;

public class DateRangeFilterModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Action { get; set; } = "Index";
    public string? Controller { get; set; }
    public string Label { get; set; } = "Khoảng thời gian";
    public bool ShowPresets { get; set; } = true;
}
