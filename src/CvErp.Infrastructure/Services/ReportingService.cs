using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Infrastructure.Services;

public class ReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<InventoryOverview> GetInventoryOverviewAsync(DateOnly from, DateOnly to)
    {
        var balances = await _db.InventoryTransactions.AsNoTracking()
            .GroupBy(x => new { x.ItemId, x.WarehouseId })
            .Select(g => new
            {
                g.Key.ItemId,
                g.Key.WarehouseId,
                Quantity = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        var items = await _db.Items.AsNoTracking().Where(x => x.IsActive).ToDictionaryAsync(x => x.Id);
        var warehouses = await _db.Warehouses.AsNoTracking().ToDictionaryAsync(x => x.Id);

        var onHandByItem = balances
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var lowStock = items.Values
            .Where(x => x.MinStock > 0)
            .Select(x =>
            {
                onHandByItem.TryGetValue(x.Id, out var onHand);
                return new LowStockRow(x.Id, x.Code, x.Name, x.Unit, onHand, x.MinStock, x.MinStock - onHand);
            })
            .Where(x => x.OnHand < x.MinStock)
            .OrderByDescending(x => x.Shortage)
            .ToList();

        var transactions = await _db.InventoryTransactions.AsNoTracking()
            .Where(x => x.TransactionDate >= from && x.TransactionDate <= to)
            .Select(x => new { x.TransactionDate, x.Quantity, x.Amount })
            .ToListAsync();

        var months = BuildMonths(from, to);
        var stockFlow = months.Select(m => new StockFlowPoint(
            m.Label,
            transactions.Where(x => x.TransactionDate.Year == m.Month.Year
                                    && x.TransactionDate.Month == m.Month.Month
                                    && x.Quantity > 0)
                .Sum(x => Math.Abs(x.Amount)),
            transactions.Where(x => x.TransactionDate.Year == m.Month.Year
                                    && x.TransactionDate.Month == m.Month.Month
                                    && x.Quantity < 0)
                .Sum(x => Math.Abs(x.Amount)))).ToList();

        var issueLines = await _db.MaterialIssueLines.AsNoTracking()
            .Include(x => x.Item)
            .Include(x => x.MaterialIssue)
            .Where(x => x.MaterialIssue!.Status == DocumentStatus.Posted
                        && x.MaterialIssue.IssueDate >= from
                        && x.MaterialIssue.IssueDate <= to)
            .ToListAsync();

        var topConsumed = issueLines
            .GroupBy(x => new { x.ItemId, x.Item!.Code, x.Item.Name, x.Item.Unit })
            .Select(g => new TopConsumedRow(g.Key.Code, g.Key.Name, g.Key.Unit,
                g.Sum(x => x.Quantity), g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Quantity)
            .Take(8)
            .ToList();

        var stockByWarehouse = balances
            .GroupBy(x => x.WarehouseId)
            .Select(g => new StockByWarehouseRow(
                warehouses.TryGetValue(g.Key, out var warehouse) ? warehouse.Name : $"Kho #{g.Key}",
                g.Where(x => x.Quantity > 0).Sum(x => x.Quantity),
                g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Value)
            .ToList();

        var recent = await _db.InventoryTransactions.AsNoTracking()
            .Include(x => x.Item)
            .Include(x => x.Warehouse)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync();

        return new InventoryOverview
        {
            TotalStockValue = balances.Sum(x => x.Amount),
            ItemCount = items.Count,
            RawMaterialCount = items.Values.Count(x => x.Type == ItemType.RawMaterial),
            LowStockCount = lowStock.Count,
            StockFlow = stockFlow,
            TopConsumed = topConsumed,
            StockByWarehouse = stockByWarehouse,
            LowStock = lowStock.Take(12).ToList(),
            RecentTransactions = recent.Select(x => new RecentTransactionRow(
                x.Id, x.TransactionDate, x.ReferenceNo, x.Item?.Name ?? "", x.Warehouse?.Name ?? "",
                x.Type, x.Quantity, x.Amount)).ToList()
        };
    }

    public async Task<ProductionOverview> GetProductionOverviewAsync(DateOnly from, DateOnly to)
    {
        var plannedOrders = await _db.ProductionOrders.AsNoTracking()
            .Where(x => x.Status != ProductionOrderStatus.Cancelled
                        && x.DueDate >= from && x.DueDate <= to)
            .Select(x => new { x.PlannedQuantity, x.DueDate })
            .ToListAsync();

        var outputs = await _db.ProductionOutputs.AsNoTracking()
            .Include(x => x.ProductionOrder)
            .Where(x => x.OutputDate >= from && x.OutputDate <= to)
            .Select(x => new
            {
                x.OutputDate,
                x.GoodQuantity,
                x.DefectQuantity,
                PlannedQuantity = x.ProductionOrder!.PlannedQuantity
            })
            .ToListAsync();

        var statusCountsRaw = await _db.ProductionOrders.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var allStatuses = Enum.GetValues<ProductionOrderStatus>()
            .Select(s => new StatusCount(s, statusCountsRaw.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
            .ToList();

        var plannedInPeriod = plannedOrders.Sum(x => x.PlannedQuantity);
        var actualInPeriod = outputs.Sum(x => x.GoodQuantity);
        var defectInPeriod = outputs.Sum(x => x.DefectQuantity);

        var completedInPeriod = await _db.ProductionOrders.AsNoTracking()
            .Where(x => x.Status == ProductionOrderStatus.Completed
                        && x.CompletedAt != null
                        && x.CompletedAt >= from.ToDateTime(TimeOnly.MinValue)
                        && x.CompletedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue))
            .CountAsync();

        var upcoming = await _db.ProductionOrders.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .Where(x => (x.Status == ProductionOrderStatus.Submitted
                         || x.Status == ProductionOrderStatus.Approved
                         || x.Status == ProductionOrderStatus.InProgress)
                        && x.DueDate >= DateOnly.FromDateTime(DateTime.Today))
            .OrderBy(x => x.DueDate)
            .Take(6)
            .ToListAsync();

        var recentOrders = await _db.ProductionOrders.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .Where(x => x.Status == ProductionOrderStatus.Completed && x.CompletedAt != null)
            .OrderByDescending(x => x.CompletedAt)
            .Take(6)
            .ToListAsync();

        var months = BuildMonths(from, to);
        var monthly = months.Select(m => new MonthlyOutputPoint(
            m.Label,
            plannedOrders.Where(x => x.DueDate.Year == m.Month.Year && x.DueDate.Month == m.Month.Month)
                .Sum(x => x.PlannedQuantity),
            outputs.Where(x => x.OutputDate.Year == m.Month.Year && x.OutputDate.Month == m.Month.Month)
                .Sum(x => x.GoodQuantity),
            outputs.Where(x => x.OutputDate.Year == m.Month.Year && x.OutputDate.Month == m.Month.Month)
                .Sum(x => x.DefectQuantity))).ToList();

        return new ProductionOverview
        {
            PlannedInPeriod = plannedInPeriod,
            ActualInPeriod = actualInPeriod,
            DefectInPeriod = defectInPeriod,
            CompletionRate = plannedInPeriod > 0 ? actualInPeriod / plannedInPeriod * 100 : 0,
            DefectRate = actualInPeriod + defectInPeriod > 0
                ? defectInPeriod / (actualInPeriod + defectInPeriod) * 100
                : 0,
            InProgressCount = allStatuses.First(x => x.Status == ProductionOrderStatus.InProgress).Count,
            PendingApprovalCount = allStatuses.First(x => x.Status == ProductionOrderStatus.Submitted).Count,
            CompletedOrderCount = completedInPeriod,
            StatusCounts = allStatuses,
            OutputByMonth = monthly,
            UpcomingOrders = upcoming.Select(MapOrder).ToList(),
            RecentOrders = recentOrders.Select(MapOrder).ToList()
        };
    }

    public async Task<VarianceOverview> GetVarianceOverviewAsync(DateOnly from, DateOnly to)
    {
        var orders = await _db.ProductionOrders.AsNoTracking()
            .Include(x => x.FinishedGoodItem)
            .Include(x => x.Materials).ThenInclude(x => x.Item)
            .Include(x => x.MaterialIssues).ThenInclude(x => x.Lines).ThenInclude(x => x.Item)
            .Where(x => x.Status == ProductionOrderStatus.Approved
                        || x.Status == ProductionOrderStatus.InProgress
                        || x.Status == ProductionOrderStatus.Completed)
            .Where(x => x.StartDate >= from && x.StartDate <= to)
            .ToListAsync();

        var standardTotals = new Dictionary<int, MaterialAggregate>();
        var actualTotals = new Dictionary<int, MaterialAggregate>();
        var orderRows = new List<OrderVarianceRow>();
        var monthlyMap = new Dictionary<(int Year, int Month), (decimal Standard, decimal Actual)>();

        decimal totalStandard = 0;
        decimal totalActual = 0;

        foreach (var order in orders)
        {
            var standardCost = order.Materials.Sum(x => x.StandardAmount);
            var postedLines = order.MaterialIssues
                .Where(x => x.Status == DocumentStatus.Posted)
                .SelectMany(x => x.Lines)
                .ToList();
            var actualCost = postedLines.Sum(x => x.Amount);

            totalStandard += standardCost;
            totalActual += actualCost;

            orderRows.Add(new OrderVarianceRow(order.Id, order.OrderNo,
                order.FinishedGoodItem?.Name ?? "", order.PlannedQuantity,
                standardCost, actualCost, order.Status));

            if (order.CompletedAt is { } completedAt)
            {
                var key = (completedAt.Year, completedAt.Month);
                monthlyMap.TryGetValue(key, out var current);
                monthlyMap[key] = (current.Standard + standardCost, current.Actual + actualCost);
            }

            foreach (var material in order.Materials)
            {
                if (material.Item is null)
                {
                    continue;
                }

                if (!standardTotals.TryGetValue(material.ItemId, out var std))
                {
                    std = new MaterialAggregate(material.Item);
                    standardTotals[material.ItemId] = std;
                }

                std.Quantity += material.StandardQuantity;
                std.Amount += material.StandardAmount;
            }

            foreach (var line in postedLines)
            {
                if (line.Item is null)
                {
                    continue;
                }

                if (!actualTotals.TryGetValue(line.ItemId, out var act))
                {
                    act = new MaterialAggregate(line.Item);
                    actualTotals[line.ItemId] = act;
                }

                act.Quantity += line.Quantity;
                act.Amount += line.Amount;
            }
        }

        var allItemIds = standardTotals.Keys.Union(actualTotals.Keys).ToList();
        var varianceRows = new List<MaterialVarianceRow>();
        foreach (var itemId in allItemIds)
        {
            standardTotals.TryGetValue(itemId, out var std);
            actualTotals.TryGetValue(itemId, out var act);
            var item = std?.Item ?? act!.Item;
            varianceRows.Add(new MaterialVarianceRow(
                item.Code, item.Name, item.Unit,
                std?.Quantity ?? 0, act?.Quantity ?? 0,
                std?.Amount ?? 0, act?.Amount ?? 0));
        }

        var months = BuildMonths(from, to);
        var monthly = months.Select(m =>
        {
            monthlyMap.TryGetValue((m.Month.Year, m.Month.Month), out var value);
            return new MonthlyVariancePoint(m.Label, value.Standard, value.Actual);
        }).ToList();

        return new VarianceOverview
        {
            StandardCost = totalStandard,
            ActualCost = totalActual,
            Variance = totalActual - totalStandard,
            VariancePercent = totalStandard > 0 ? (totalActual - totalStandard) / totalStandard * 100 : 0,
            OrderCount = orders.Count,
            OverrunItemCount = varianceRows.Count(x => x.QuantityVariance > 0),
            TopOverruns = varianceRows
                .Where(x => x.QuantityVariance > 0 || x.AmountVariance > 0)
                .OrderByDescending(x => x.AmountVariance)
                .Take(10)
                .ToList(),
            Orders = orderRows.OrderByDescending(x => x.VariancePercent).ToList(),
            Monthly = monthly
        };
    }

    private static ProductionOrderSummary MapOrder(ProductionOrder order) => new(
        order.Id, order.OrderNo, order.FinishedGoodItem?.Code ?? "", order.FinishedGoodItem?.Name ?? "",
        order.PlannedQuantity, order.ActualQuantity, order.DefectQuantity, order.Status,
        order.StartDate, order.DueDate, order.CompletedAt);

    private static List<(DateOnly Month, string Label)> BuildMonths(DateOnly from, DateOnly to)
    {
        var start = new DateOnly(from.Year, from.Month, 1);
        var end = new DateOnly(to.Year, to.Month, 1);
        var result = new List<(DateOnly, string)>();
        for (var current = start; current <= end && result.Count < 24; current = current.AddMonths(1))
        {
            result.Add((current, current.ToString("MM/yyyy")));
        }

        return result;
    }

    private sealed class MaterialAggregate
    {
        public MaterialAggregate(Item item)
        {
            Item = item;
        }

        public Item Item { get; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
    }
}
