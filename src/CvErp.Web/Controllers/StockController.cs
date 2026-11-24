using CvErp.Domain.Enums;
using CvErp.Infrastructure.Data;
using CvErp.Web.Helpers;
using CvErp.Web.ViewModels.Common;
using CvErp.Web.ViewModels.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Web.Controllers;

public class StockController : BaseController
{
    private readonly AppDbContext _db;

    public StockController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, int? warehouseId, string? category, bool lowStockOnly = false,
        int page = 1)
    {
        var ledger = await _db.InventoryTransactions.AsNoTracking()
            .GroupBy(x => new { x.ItemId, x.WarehouseId })
            .Select(g => new
            {
                g.Key.ItemId,
                g.Key.WarehouseId,
                Quantity = g.Sum(x => x.Quantity),
                Value = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        var items = await _db.Items.AsNoTracking().ToDictionaryAsync(x => x.Id);
        var warehouses = await _db.Warehouses.AsNoTracking().ToDictionaryAsync(x => x.Id);

        var totalsByItem = ledger.GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var allRows = ledger.Select(x =>
        {
            items.TryGetValue(x.ItemId, out var item);
            warehouses.TryGetValue(x.WarehouseId, out var warehouse);
            totalsByItem.TryGetValue(x.ItemId, out var totalQuantity);
            return new StockBalanceRow
            {
                ItemId = x.ItemId,
                Code = item?.Code ?? "",
                Name = item?.Name ?? "",
                Unit = item?.Unit ?? "",
                Category = item?.Category ?? "",
                ItemType = item?.Type ?? ItemType.RawMaterial,
                WarehouseId = x.WarehouseId,
                WarehouseName = warehouse?.Name ?? $"Kho #{x.WarehouseId}",
                Quantity = x.Quantity,
                UnitCost = x.Quantity != 0 ? decimal.Round(x.Value / x.Quantity, 4) : (item?.StandardCost ?? 0),
                Value = x.Value,
                MinStock = item?.MinStock ?? 0,
                TotalQuantity = totalQuantity
            };
        }).ToList();

        var lowStockItemIds = allRows.Where(x => x.MinStock > 0 && x.TotalQuantity < x.MinStock)
            .Select(x => x.ItemId)
            .Distinct()
            .ToHashSet();

        IEnumerable<StockBalanceRow> filtered = allRows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            filtered = filtered.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword));
        }

        if (warehouseId is not null)
        {
            filtered = filtered.Where(x => x.WarehouseId == warehouseId);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(x => x.Category == category);
        }

        if (lowStockOnly)
        {
            filtered = filtered.Where(x => lowStockItemIds.Contains(x.ItemId));
        }

        var rows = filtered
            .OrderBy(x => x.Code)
            .ThenBy(x => x.WarehouseName)
            .ToList();

        var pageSize = 20;
        var totalPages = rows.Count == 0 ? 0 : (int)Math.Ceiling(rows.Count / (double)pageSize);
        page = Math.Max(1, page);
        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        var model = new StockBalanceViewModel
        {
            Search = search,
            WarehouseId = warehouseId,
            Category = category,
            LowStockOnly = lowStockOnly,
            WarehouseOptions = await ErpLookups.WarehouseOptionsAsync(_db, false, warehouseId),
            Categories = await _db.Items.AsNoTracking()
                .Where(x => x.Category != "")
                .Select(x => x.Category)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(),
            Rows = new PagedResult<StockBalanceRow>
            {
                Items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = rows.Count
            },
            TotalQuantity = rows.Sum(x => x.Quantity),
            TotalValue = rows.Sum(x => x.Value),
            LowStockCount = lowStockItemIds.Count,
            ItemCount = rows.Select(x => x.ItemId).Distinct().Count()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Card(int? itemId, int? warehouseId, DateOnly? from, DateOnly? to)
    {
        var items = await _db.Items.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        var warehouses = await _db.Warehouses.AsNoTracking().OrderBy(x => x.Code).ToListAsync();

        if (items.Count == 0 || warehouses.Count == 0)
        {
            return View(new StockCardViewModel());
        }

        var selectedItem = items.FirstOrDefault(x => x.Id == itemId) ?? items.First();
        var selectedWarehouse = warehouses.FirstOrDefault(x => x.Id == warehouseId) ?? warehouses.First();
        var (start, end) = ResolveRange(from, to, 6);

        var ledgerQuery = _db.InventoryTransactions.AsNoTracking()
            .Where(x => x.ItemId == selectedItem.Id && x.WarehouseId == selectedWarehouse.Id);

        var opening = await ledgerQuery
            .Where(x => x.TransactionDate < start)
            .GroupBy(x => 1)
            .Select(g => new { Quantity = g.Sum(x => x.Quantity), Amount = g.Sum(x => x.Amount) })
            .FirstOrDefaultAsync();

        var transactions = await ledgerQuery
            .Where(x => x.TransactionDate >= start && x.TransactionDate <= end)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var running = opening?.Quantity ?? 0;
        var rows = new List<StockCardRow>();
        foreach (var transaction in transactions)
        {
            running += transaction.Quantity;
            rows.Add(new StockCardRow
            {
                Id = transaction.Id,
                Date = transaction.TransactionDate,
                ReferenceNo = transaction.ReferenceNo,
                Type = transaction.Type,
                Quantity = transaction.Quantity,
                UnitCost = transaction.UnitCost,
                Amount = transaction.Amount,
                RunningQuantity = running,
                Note = transaction.Note
            });
        }

        var closingAmount = (opening?.Amount ?? 0) + transactions.Sum(x => x.Amount);

        var model = new StockCardViewModel
        {
            Item = selectedItem,
            Warehouse = selectedWarehouse,
            ItemId = selectedItem.Id,
            WarehouseId = selectedWarehouse.Id,
            From = start,
            To = end,
            OpeningQuantity = opening?.Quantity ?? 0,
            OpeningValue = opening?.Amount ?? 0,
            ClosingQuantity = running,
            ClosingValue = closingAmount,
            Rows = rows,
            ItemOptions = items.Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                $"{x.Code} · {x.Name} ({x.Unit})", x.Id.ToString(), x.Id == selectedItem.Id)).ToList(),
            WarehouseOptions = warehouses.Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                $"{x.Code} · {x.Name}", x.Id.ToString(), x.Id == selectedWarehouse.Id)).ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> ExportCard(int itemId, int warehouseId, DateOnly? from, DateOnly? to)
    {
        var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId);
        var warehouse = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == warehouseId);
        if (item is null || warehouse is null)
        {
            return NotFound();
        }

        var (start, end) = ResolveRange(from, to, 6);
        var ledgerQuery = _db.InventoryTransactions.AsNoTracking()
            .Where(x => x.ItemId == itemId && x.WarehouseId == warehouseId);

        var opening = await ledgerQuery.Where(x => x.TransactionDate < start)
            .GroupBy(x => 1)
            .Select(g => new { Quantity = g.Sum(x => x.Quantity), Amount = g.Sum(x => x.Amount) })
            .FirstOrDefaultAsync();

        var transactions = await ledgerQuery
            .Where(x => x.TransactionDate >= start && x.TransactionDate <= end)
            .OrderBy(x => x.TransactionDate).ThenBy(x => x.Id)
            .ToListAsync();

        var rows = new List<IReadOnlyList<object?>>();
        rows.Add(new object?[] { "Số dư đầu kỳ", "", "", "", opening?.Quantity ?? 0, "", opening?.Amount ?? 0, "" });
        var running = opening?.Quantity ?? 0;
        foreach (var transaction in transactions)
        {
            running += transaction.Quantity;
            rows.Add(new object?[]
            {
                transaction.TransactionDate, transaction.ReferenceNo, UiText.TxTypeLabel(transaction.Type),
                transaction.Note ?? "", transaction.Quantity, transaction.UnitCost, transaction.Amount, running
            });
        }

        return Csv.Export($"the-kho-{item.Code}-{warehouse.Code}-{DateTime.Now:yyyyMMdd}.csv",
            new[] { "Ngày", "Số chứng từ", "Loại giao dịch", "Diễn giải", "Nhập/Xuất", "Đơn giá", "Giá trị", "Tồn lũy kế" },
            rows);
    }

    [HttpGet]
    public async Task<IActionResult> ItemInfo(int itemId)
    {
        var item = await _db.Items.AsNoTracking()
            .Where(x => x.Id == itemId)
            .Select(x => new { x.Id, x.Code, x.Name, x.Unit, x.StandardCost })
            .FirstOrDefaultAsync();

        return item is null
            ? Json(new { found = false })
            : Json(new { found = true, item.Id, item.Code, item.Name, item.Unit, item.StandardCost });
    }

    [HttpGet]
    public async Task<IActionResult> Availability(int warehouseId, int itemId)
    {
        if (warehouseId <= 0 || itemId <= 0)
        {
            return Json(new { quantity = 0m });
        }

        var quantity = await _db.InventoryTransactions.AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && x.ItemId == itemId)
            .SumAsync(x => (decimal?)x.Quantity) ?? 0m;

        return Json(new { quantity });
    }
}
