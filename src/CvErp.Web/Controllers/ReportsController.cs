using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Reporting;
using CvErp.Infrastructure.Services;
using CvErp.Web.Helpers;
using CvErp.Web.ViewModels.Common;
using CvErp.Web.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

public class ReportsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly ReportingService _reporting;

    public ReportsController(AppDbContext db, ReportingService reporting)
    {
        _db = db;
        _reporting = reporting;
    }

    public async Task<IActionResult> Index()
    {
        var (start, end) = ResolveRange(null, null);
        var inventory = await _reporting.GetInventoryOverviewAsync(start, end);
        ViewData["From"] = start;
        ViewData["To"] = end;
        return View(inventory);
    }

    public async Task<IActionResult> StockSummary(int? warehouseId, string? category)
    {
        var model = await BuildStockSummaryAsync(warehouseId, category);
        return View(model);
    }

    public async Task<IActionResult> Movements(DateOnly? from, DateOnly? to, int? warehouseId, int? itemId,
        InventoryTransactionType? type, int page = 1)
    {
        var (start, end) = ResolveRange(from, to, 3);
        var query = _db.InventoryTransactions.AsNoTracking()
            .Include(x => x.Item)
            .Include(x => x.Warehouse)
            .Where(x => x.TransactionDate >= start && x.TransactionDate <= end);

        if (warehouseId is not null) query = query.Where(x => x.WarehouseId == warehouseId);
        if (itemId is not null) query = query.Where(x => x.ItemId == itemId);
        if (type is not null) query = query.Where(x => x.Type == type);

        var inQuantity = await query.Where(x => x.Quantity > 0).SumAsync(x => (decimal?)x.Quantity) ?? 0;
        var outQuantity = await query.Where(x => x.Quantity < 0).SumAsync(x => (decimal?)x.Quantity) ?? 0;
        var inValue = await query.Where(x => x.Quantity > 0).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var outValue = await query.Where(x => x.Quantity < 0).SumAsync(x => (decimal?)x.Amount) ?? 0;

        var model = new MovementReportViewModel
        {
            From = start,
            To = end,
            WarehouseId = warehouseId,
            ItemId = itemId,
            Type = type,
            WarehouseOptions = await ErpLookups.WarehouseOptionsAsync(_db, false, warehouseId),
            ItemOptions = await ErpLookups.ItemOptionsAsync(_db, false, null, itemId),
            Transactions = await query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id)
                .ToPagedResultAsync(page, 20),
            TotalInQuantity = inQuantity,
            TotalOutQuantity = outQuantity,
            TotalInValue = inValue,
            TotalOutValue = outValue
        };

        return View(model);
    }

    public async Task<IActionResult> LowStock()
    {
        var rows = await GetLowStockAsync();
        var items = await _db.Items.AsNoTracking()
            .Where(x => rows.Select(r => r.ItemId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        return View(new LowStockReportViewModel
        {
            Rows = rows,
            ItemCount = rows.Count,
            TotalShortageValue = rows.Sum(x =>
                x.Shortage * (items.TryGetValue(x.ItemId, out var item) ? item.StandardCost : 0))
        });
    }

    public async Task<IActionResult> Production(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var data = await _reporting.GetProductionOverviewAsync(start, end);
        return View(new ProductionReportViewModel { From = start, To = end, Data = data });
    }

    [Authorize(Roles = "Administrator,Manager")]
    public async Task<IActionResult> Variance(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var data = await _reporting.GetVarianceOverviewAsync(start, end);
        return View(new VarianceReportViewModel { From = start, To = end, Data = data });
    }

    [Authorize(Roles = "Administrator,Manager")]
    public async Task<IActionResult> ExportVariance(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var data = await _reporting.GetVarianceOverviewAsync(start, end);

        return Csv.Export($"chenh-lech-chi-phi-{start:yyyyMMdd}-{end:yyyyMMdd}.csv",
            new[] { "Số lệnh", "Thành phẩm", "SL kế hoạch", "Chi phí định mức", "Chi phí thực tế", "Chênh lệch", "Tỷ lệ %", "Trạng thái" },
            data.Orders.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                x.OrderNo, x.ProductName, x.PlannedQuantity, x.StandardCost, x.ActualCost, x.Variance,
                Math.Round(x.VariancePercent, 1), UiText.ProductionStatusLabel(x.Status)
            }));
    }

    public async Task<IActionResult> ExportStockSummary(int? warehouseId, string? category)
    {
        var model = await BuildStockSummaryAsync(warehouseId, category);
        return Csv.Export($"tong-hop-ton-kho-{DateTime.Now:yyyyMMdd}.csv",
            new[] { "Mã vật tư", "Tên vật tư", "ĐVT", "Nhóm", "Loại", "Số lượng", "Đơn giá", "Giá trị", "Tồn tối thiểu", "Cảnh báo" },
            model.Rows.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                x.Code, x.Name, x.Unit, x.Category, UiText.ItemTypeLabel(x.ItemType), x.QuantityByWarehouse,
                x.UnitCost, x.Value, x.MinStock, x.IsLowStock ? "Dưới định mức" : ""
            }));
    }

    public async Task<IActionResult> ExportMovements(DateOnly? from, DateOnly? to, int? warehouseId, int? itemId,
        InventoryTransactionType? type)
    {
        var (start, end) = ResolveRange(from, to, 3);
        var query = _db.InventoryTransactions.AsNoTracking()
            .Include(x => x.Item)
            .Include(x => x.Warehouse)
            .Where(x => x.TransactionDate >= start && x.TransactionDate <= end);

        if (warehouseId is not null) query = query.Where(x => x.WarehouseId == warehouseId);
        if (itemId is not null) query = query.Where(x => x.ItemId == itemId);
        if (type is not null) query = query.Where(x => x.Type == type);

        var rows = await query.OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).Take(10000).ToListAsync();
        return Csv.Export($"bien-dong-kho-{start:yyyyMMdd}-{end:yyyyMMdd}.csv",
            new[] { "Ngày", "Mã vật tư", "Tên vật tư", "Kho", "Loại giao dịch", "Số chứng từ", "Số lượng", "Đơn giá", "Giá trị", "Ghi chú" },
            rows.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                x.TransactionDate, x.Item?.Code ?? "", x.Item?.Name ?? "", x.Warehouse?.Name ?? "",
                UiText.TxTypeLabel(x.Type), x.ReferenceNo, x.Quantity, x.UnitCost, x.Amount, x.Note ?? ""
            }));
    }

    public async Task<IActionResult> ExportLowStock()
    {
        var rows = await GetLowStockAsync();
        return Csv.Export($"vat-tu-duoi-dinh-muc-{DateTime.Now:yyyyMMdd}.csv",
            new[] { "Mã vật tư", "Tên vật tư", "ĐVT", "Tồn hiện tại", "Tồn tối thiểu", "Thiếu hụt" },
            rows.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                x.Code, x.Name, x.Unit, x.OnHand, x.MinStock, x.Shortage
            }));
    }

    public async Task<IActionResult> ExportProduction(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var data = await _reporting.GetProductionOverviewAsync(start, end);
        return Csv.Export($"san-luong-theo-thang-{start:yyyyMMdd}-{end:yyyyMMdd}.csv",
            new[] { "Tháng", "Kế hoạch", "Sản lượng tốt", "Sản lượng lỗi" },
            data.OutputByMonth.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                x.Month, x.Planned, x.Actual, x.Defect
            }));
    }

    private async Task<StockSummaryReportViewModel> BuildStockSummaryAsync(int? warehouseId, string? category)
    {
        var balances = await _db.InventoryTransactions.AsNoTracking()
            .GroupBy(x => new { x.ItemId, x.WarehouseId })
            .Select(g => new
            {
                g.Key.ItemId,
                g.Key.WarehouseId,
                Quantity = g.Sum(x => x.Quantity),
                Value = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        var items = await _db.Items.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        var categories = items.Where(x => x.Category != "").Select(x => x.Category).Distinct().OrderBy(x => x).ToList();

        var filteredItems = items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            filteredItems = filteredItems.Where(x => x.Category == category);
        }

        var rows = new List<StockSummaryRow>();
        foreach (var item in filteredItems)
        {
            var itemBalances = balances.Where(x => x.ItemId == item.Id).ToList();
            var totalQuantity = itemBalances.Sum(x => x.Quantity);
            var scoped = warehouseId is null
                ? itemBalances
                : itemBalances.Where(x => x.WarehouseId == warehouseId).ToList();
            var quantity = scoped.Sum(x => x.Quantity);
            var value = scoped.Sum(x => x.Value);

            if (totalQuantity == 0 && quantity == 0)
            {
                continue;
            }

            rows.Add(new StockSummaryRow
            {
                Code = item.Code,
                Name = item.Name,
                Unit = item.Unit,
                Category = item.Category,
                ItemType = item.Type,
                QuantityByWarehouse = quantity,
                TotalQuantity = totalQuantity,
                UnitCost = quantity != 0 ? decimal.Round(value / quantity, 4) : item.StandardCost,
                Value = value,
                MinStock = item.MinStock
            });
        }

        return new StockSummaryReportViewModel
        {
            WarehouseId = warehouseId,
            Category = category,
            WarehouseOptions = await ErpLookups.WarehouseOptionsAsync(_db, false, warehouseId),
            Categories = categories,
            Rows = rows,
            TotalQuantity = rows.Sum(x => x.QuantityByWarehouse),
            TotalValue = rows.Sum(x => x.Value),
            ItemCount = rows.Select(x => x.Code).Distinct().Count(),
            LowStockCount = rows.Count(x => x.IsLowStock)
        };
    }

    private async Task<List<LowStockRow>> GetLowStockAsync()
    {
        var totals = await _db.InventoryTransactions.AsNoTracking()
            .GroupBy(x => x.ItemId)
            .Select(g => new { ItemId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Quantity);

        var items = await _db.Items.AsNoTracking()
            .Where(x => x.IsActive && x.MinStock > 0)
            .OrderBy(x => x.Code)
            .ToListAsync();

        return items.Select(item =>
        {
            totals.TryGetValue(item.Id, out var onHand);
            return new LowStockRow(item.Id, item.Code, item.Name, item.Unit, onHand, item.MinStock,
                item.MinStock - onHand);
        }).Where(x => x.OnHand < x.MinStock)
        .OrderByDescending(x => x.Shortage)
        .ToList();
    }
}
