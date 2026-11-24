using System.Globalization;
using CvErp.Domain.Enums;

namespace CvErp.Web.Helpers;

public static class UiText
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Money(decimal value) => value.ToString("#,##0", Vi);

    public static string Money4(decimal value) => value.ToString("#,##0.####", Vi);

    public static string Qty(decimal value) => value.ToString("#,##0.####", Vi);

    public static string Percent(decimal value) => value.ToString("0.0", Vi) + "%";

    public static string Date(DateOnly? value) => value?.ToString("dd/MM/yyyy") ?? "—";

    public static string Date(DateOnly value) => value.ToString("dd/MM/yyyy");

    public static string DateTimeLocal(DateTime? value)
    {
        if (value is null)
        {
            return "—";
        }

        var utc = System.DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        return utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Vi);
    }

    public static string DateLocal(DateTime? value)
    {
        if (value is null)
        {
            return "—";
        }

        var utc = System.DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        return utc.ToLocalTime().ToString("dd/MM/yyyy", Vi);
    }

    public static string StatusLabel(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "Nháp",
        DocumentStatus.Submitted => "Chờ duyệt",
        DocumentStatus.Approved => "Đã duyệt",
        DocumentStatus.Posted => "Đã ghi sổ",
        DocumentStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    public static string StatusCss(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "text-bg-secondary",
        DocumentStatus.Submitted => "text-bg-warning",
        DocumentStatus.Approved => "text-bg-info",
        DocumentStatus.Posted => "text-bg-success",
        DocumentStatus.Cancelled => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string ProductionStatusLabel(ProductionOrderStatus status) => status switch
    {
        ProductionOrderStatus.Draft => "Nháp",
        ProductionOrderStatus.Submitted => "Chờ duyệt",
        ProductionOrderStatus.Approved => "Đã duyệt",
        ProductionOrderStatus.InProgress => "Đang sản xuất",
        ProductionOrderStatus.Completed => "Hoàn thành",
        ProductionOrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    public static string ProductionStatusCss(ProductionOrderStatus status) => status switch
    {
        ProductionOrderStatus.Draft => "text-bg-secondary",
        ProductionOrderStatus.Submitted => "text-bg-warning",
        ProductionOrderStatus.Approved => "text-bg-info",
        ProductionOrderStatus.InProgress => "text-bg-primary",
        ProductionOrderStatus.Completed => "text-bg-success",
        ProductionOrderStatus.Cancelled => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string DocTypeLabel(StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => "Nhập kho",
        StockDocumentType.Issue => "Xuất kho",
        StockDocumentType.Transfer => "Điều chuyển",
        _ => type.ToString()
    };

    public static string DocTypeCss(StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => "text-bg-success",
        StockDocumentType.Issue => "text-bg-warning",
        _ => "text-bg-primary"
    };

    public static string ItemTypeLabel(ItemType type) => type switch
    {
        ItemType.RawMaterial => "Nguyên vật liệu",
        ItemType.FinishedGood => "Thành phẩm",
        _ => type.ToString()
    };

    public static string TxTypeLabel(InventoryTransactionType type) => type switch
    {
        InventoryTransactionType.Opening => "Tồn đầu kỳ",
        InventoryTransactionType.GoodsReceipt => "Nhập kho",
        InventoryTransactionType.GoodsIssue => "Xuất kho",
        InventoryTransactionType.TransferIn => "Nhận điều chuyển",
        InventoryTransactionType.TransferOut => "Xuất điều chuyển",
        InventoryTransactionType.ProductionIssue => "Cấp phát sản xuất",
        InventoryTransactionType.ProductionReceipt => "Nhập thành phẩm",
        InventoryTransactionType.AdjustmentIn => "Điều chỉnh tăng",
        InventoryTransactionType.AdjustmentOut => "Điều chỉnh giảm",
        _ => type.ToString()
    };

    public static string RoleLabel(string role) => role switch
    {
        "Administrator" => "Quản trị viên",
        "Manager" => "Quản lý",
        "Warehouse" => "Thủ kho",
        "Production" => "Sản xuất",
        "Purchasing" => "Mua hàng",
        _ => role
    };

    public static string ActionLabel(string action) => action switch
    {
        "LOGIN" => "Đăng nhập",
        "LOGIN_FAILED" => "Đăng nhập thất bại",
        "CREATE" => "Tạo mới",
        "UPDATE" => "Cập nhật",
        "SUBMIT" => "Trình duyệt",
        "APPROVE" => "Phê duyệt",
        "POST" => "Ghi sổ",
        "CANCEL" => "Hủy",
        "COMPLETE" => "Hoàn thành",
        "SEED" => "Khởi tạo dữ liệu",
        _ => action
    };

    public static string EntityLabel(string? entity) => entity switch
    {
        "Account" => "Tài khoản",
        "ApplicationUser" => "Người dùng",
        "StockDocument" => "Chứng từ kho",
        "MaterialIssue" => "Phiếu cấp phát",
        "ProductionOrder" => "Lệnh sản xuất",
        "ProductionOutput" => "Sản lượng",
        "InventoryTransaction" => "Giao dịch kho",
        "Item" => "Vật tư",
        "Warehouse" => "Kho hàng",
        "Supplier" => "Nhà cung cấp",
        "Bom" => "Định mức BOM",
        _ => entity ?? "—"
    };

    public static string Badge(string label, string cssClass)
        => $"<span class=\"badge rounded-pill {cssClass}\">{System.Net.WebUtility.HtmlEncode(label)}</span>";
}
