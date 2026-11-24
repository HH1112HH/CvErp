using CvErp.Domain.Constants;
using CvErp.Domain.Entities;
using CvErp.Domain.Enums;
using CvErp.Infrastructure.Common;
using CvErp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Infrastructure.Data;

/// <summary>Khởi tạo dữ liệu mẫu cho nhà máy kéo sợi (Vinatex Phú Hưng — KCN Phú Bài, Huế).</summary>
public class DbSeeder
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly Random _random = new(20261124);
    private readonly Dictionary<(string Prefix, int Year), int> _sequences = new();
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    private ApplicationUser _admin = null!;
    private ApplicationUser _manager = null!;
    private ApplicationUser _warehouse = null!;
    private ApplicationUser _production = null!;
    private ApplicationUser _purchasing = null!;

    private readonly Dictionary<string, Item> _items = new();
    private readonly Dictionary<string, Warehouse> _warehouses = new();
    private readonly Dictionary<string, Supplier> _suppliers = new();
    private readonly Dictionary<string, Bom> _boms = new();

    public DbSeeder(AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task SeedAsync()
    {
        if (await _db.Items.AnyAsync())
        {
            return;
        }

        await SeedIdentityAsync();
        await SeedMasterDataAsync();
        await SeedOpeningStockAsync();
        await SeedInventoryDocumentsAsync();
        await SeedProductionAsync();
        await SeedNumberSequencesAsync();
        await SeedAuditLogsAsync();
    }

    private async Task SeedIdentityAsync()
    {
        foreach (var role in AppRoles.All)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        _admin = await EnsureUserAsync("admin", "Admin@123", "Quản trị hệ thống", AppRoles.Administrator);
        _manager = await EnsureUserAsync("manager", "Manager@123", "Trần Minh Quân", AppRoles.Manager);
        _warehouse = await EnsureUserAsync("kho", "Kho@123", "Nguyễn Văn Khoa", AppRoles.Warehouse);
        _production = await EnsureUserAsync("sanxuat", "Sanxuat@123", "Lê Thị Hương", AppRoles.Production);
        _purchasing = await EnsureUserAsync("muahang", "Muahang@123", "Phạm Anh Tuấn", AppRoles.Purchasing);
    }

    private async Task<ApplicationUser> EnsureUserAsync(string username, string password, string fullName, string role)
    {
        var user = await _userManager.FindByNameAsync(username);
        if (user is not null)
        {
            return user;
        }

        user = new ApplicationUser
        {
            UserName = username,
            Email = $"{username}@vph-demo.vn",
            FullName = fullName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-8)
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Không tạo được tài khoản mẫu: " +
                                                string.Join("; ", result.Errors.Select(x => x.Description)));
        }

        await _userManager.AddToRoleAsync(user, role);
        return user;
    }

    private async Task SeedMasterDataAsync()
    {
        _warehouses["NVL"] = new Warehouse { Code = "KHO-NVL", Name = "Kho nguyên liệu xơ", Location = "Nhà máy 1 - KCN Phú Bài", CreatedAt = DateTime.UtcNow.AddMonths(-10) };
        _warehouses["TP"] = new Warehouse { Code = "KHO-TP", Name = "Kho thành phẩm sợi", Location = "Nhà máy 2 - KCN Phú Bài", CreatedAt = DateTime.UtcNow.AddMonths(-10) };
        _warehouses["PK"] = new Warehouse { Code = "KHO-PK", Name = "Kho phụ liệu & bao bì", Location = "Nhà máy 2 - KCN Phú Bài", CreatedAt = DateTime.UtcNow.AddMonths(-10) };
        _db.Warehouses.AddRange(_warehouses.Values);

        // Xơ cotton
        AddItem("COT-US", "Xơ cotton Mỹ", "kg", "Xơ cotton", ItemType.RawMaterial, 50_000, 52_000);
        AddItem("COT-BR", "Xơ cotton Brazil", "kg", "Xơ cotton", ItemType.RawMaterial, 40_000, 50_000);
        AddItem("COT-VN", "Xơ cotton Việt Nam", "kg", "Xơ cotton", ItemType.RawMaterial, 60_000, 46_000);
        // Xơ polyester và xơ tái chế
        AddItem("PES-14", "Xơ polyester 1.4D", "kg", "Xơ polyester", ItemType.RawMaterial, 40_000, 31_000);
        AddItem("PES-12", "Xơ polyester 1.2D", "kg", "Xơ polyester", ItemType.RawMaterial, 30_000, 32_000);
        AddItem("RECY-F", "Xơ tái chế", "kg", "Xơ tái chế", ItemType.RawMaterial, 20_000, 24_000);
        // Phụ liệu và bao bì
        AddItem("OIL-SP", "Dầu bôi trơn cọc sợi", "kg", "Hóa chất", ItemType.RawMaterial, 2_000, 85_000);
        AddItem("CONE", "Ống giấy cone", "cái", "Phụ liệu", ItemType.RawMaterial, 20_000, 7_500);
        AddItem("BAG-PP", "Bao PP đóng kiện", "cái", "Bao bì", ItemType.RawMaterial, 10_000, 12_000);
        AddItem("LABEL", "Nhãn kiện sợi", "cái", "Bao bì", ItemType.RawMaterial, 20_000, 1_500);
        AddItem("WRAP-PE", "Màng PE bọc kiện", "kg", "Bao bì", ItemType.RawMaterial, 2_000, 38_000);
        AddItem("STRAP", "Dây đai kiện", "m", "Bao bì", ItemType.RawMaterial, 5_000, 4_000);

        // Thành phẩm sợi
        AddItem("CVCM-30", "Sợi CVCM Ne 30", "kg", "Sợi cotton", ItemType.FinishedGood, 8_000, 82_000);
        AddItem("CVCD-30", "Sợi CVCD Ne 30", "kg", "Sợi cotton", ItemType.FinishedGood, 6_000, 84_000);
        AddItem("TCCM-30", "Sợi TCCM Ne 30", "kg", "Sợi pha", ItemType.FinishedGood, 6_000, 76_000);
        AddItem("TCCD-30", "Sợi TCCD Ne 30", "kg", "Sợi pha", ItemType.FinishedGood, 6_000, 78_000);
        AddItem("RECY-30", "Sợi Recycled Ne 30", "kg", "Sợi tái chế", ItemType.FinishedGood, 5_000, 68_000);
        AddItem("CVCM-26", "Sợi CVCM Ne 26", "kg", "Sợi cotton", ItemType.FinishedGood, 4_000, 80_000);
        AddItem("TCCM-26", "Sợi TCCM Ne 26", "kg", "Sợi pha", ItemType.FinishedGood, 4_000, 74_000);
        AddItem("RECY-26", "Sợi Recycled Ne 26", "kg", "Sợi tái chế", ItemType.FinishedGood, 4_000, 66_000);

        _db.Items.AddRange(_items.Values);

        _suppliers["COT"] = new Supplier
        {
            Code = "NCC-COT", Name = "Công ty CP Bông Việt Nam", TaxCode = "0301234567",
            Phone = "028 3821 2345", Email = "sales@bongvietnam.vn",
            Address = "KCN Tân Bình, Q. Tân Phú, TP.HCM"
        };
        _suppliers["PES"] = new Supplier
        {
            Code = "NCC-PES", Name = "Công ty CP Xơ sợi Polyester Đình Vũ", TaxCode = "0201234567",
            Phone = "0225 3769 888", Email = "sales@dinhvu-polyester.vn",
            Address = "KCN Đình Vũ, Hải Phòng"
        };
        _suppliers["RECY"] = new Supplier
        {
            Code = "NCC-RECY", Name = "Công ty TNHH Tái chế Sợi Xanh", TaxCode = "0312345678",
            Phone = "028 3777 6666", Email = "info@soixanh.vn",
            Address = "KCN Tân Tạo, Q. Bình Tân, TP.HCM"
        };
        _suppliers["PK"] = new Supplier
        {
            Code = "NCC-PK", Name = "Công ty TNHH Bao bì Phú Bài", TaxCode = "3301234567",
            Phone = "0234 3863 240", Email = "sales@baobiphu.vn",
            Address = "KCN Phú Bài, TP. Huế"
        };
        _suppliers["OIL"] = new Supplier
        {
            Code = "NCC-OIL", Name = "Công ty TNHH Dầu công nghiệp Miền Trung", TaxCode = "3302345678",
            Phone = "0234 3951 234", Email = "info@daucongnghiep.vn",
            Address = "KCN Hòa Khánh, Đà Nẵng"
        };
        _db.Suppliers.AddRange(_suppliers.Values);

        await _db.SaveChangesAsync();

        var bomCvcm = CreateBom("BOM-CVCM30-V1", "Định mức sợi CVCM Ne 30", _items["CVCM-30"], "1.0",
            new[]
            {
                ("COT-VN", 1.05m, 2m), ("PES-14", 0.03m, 1m), ("OIL-SP", 0.008m, 0m),
                ("CONE", 0.033m, 0m), ("BAG-PP", 0.02m, 0m), ("LABEL", 0.02m, 0m)
            });

        var bomCvcd = CreateBom("BOM-CVCD30-V1", "Định mức sợi CVCD Ne 30", _items["CVCD-30"], "1.0",
            new[]
            {
                ("COT-US", 0.55m, 2m), ("COT-BR", 0.50m, 2m), ("OIL-SP", 0.008m, 0m),
                ("CONE", 0.033m, 0m), ("BAG-PP", 0.02m, 0m), ("LABEL", 0.02m, 0m)
            });

        var bomTccm = CreateBom("BOM-TCCM30-V1", "Định mức sợi TCCM Ne 30", _items["TCCM-30"], "1.0",
            new[]
            {
                ("PES-14", 0.65m, 1m), ("COT-VN", 0.40m, 2m), ("OIL-SP", 0.008m, 0m),
                ("CONE", 0.033m, 0m), ("BAG-PP", 0.02m, 0m), ("LABEL", 0.02m, 0m)
            });

        var bomTccd = CreateBom("BOM-TCCD30-V1", "Định mức sợi TCCD Ne 30", _items["TCCD-30"], "1.0",
            new[]
            {
                ("PES-12", 0.55m, 1m), ("COT-US", 0.50m, 2m), ("OIL-SP", 0.008m, 0m),
                ("CONE", 0.033m, 0m), ("BAG-PP", 0.02m, 0m), ("LABEL", 0.02m, 0m)
            });

        var bomRecy = CreateBom("BOM-RECY30-V1", "Định mức sợi Recycled Ne 30", _items["RECY-30"], "1.0",
            new[]
            {
                ("RECY-F", 0.85m, 1m), ("COT-VN", 0.20m, 2m), ("OIL-SP", 0.008m, 0m),
                ("CONE", 0.033m, 0m), ("BAG-PP", 0.02m, 0m), ("LABEL", 0.02m, 0m)
            });

        _boms["CVCM-30"] = bomCvcm;
        _boms["CVCD-30"] = bomCvcd;
        _boms["TCCM-30"] = bomTccm;
        _boms["TCCD-30"] = bomTccd;
        _boms["RECY-30"] = bomRecy;

        _db.Boms.AddRange(_boms.Values);
        await _db.SaveChangesAsync();
    }

    private void AddItem(string code, string name, string unit, string category, ItemType type, decimal minStock, decimal cost)
    {
        _items[code] = new Item
        {
            Code = code,
            Name = name,
            Unit = unit,
            Category = category,
            Type = type,
            MinStock = minStock,
            StandardCost = cost,
            CreatedAt = DateTime.UtcNow.AddMonths(-10)
        };
    }

    private Bom CreateBom(string code, string name, Item finishedGood, string version,
        IEnumerable<(string ItemCode, decimal Qty, decimal Scrap)> lines)
    {
        var bom = new Bom
        {
            Code = code,
            Name = name,
            FinishedGoodItem = finishedGood,
            Version = version,
            CreatedAt = DateTime.UtcNow.AddMonths(-9)
        };

        foreach (var line in lines)
        {
            bom.Lines.Add(new BomLine
            {
                ComponentItem = _items[line.ItemCode],
                QuantityPerUnit = line.Qty,
                ScrapPercent = line.Scrap
            });
        }

        return bom;
    }

    private async Task SeedOpeningStockAsync()
    {
        var openingDate = MonthStart(-11).AddDays(3);
        var opening = new (string Code, decimal Qty, string Warehouse)[]
        {
            ("COT-US", 700_000, "NVL"), ("COT-BR", 500_000, "NVL"), ("COT-VN", 600_000, "NVL"),
            ("PES-14", 400_000, "NVL"), ("PES-12", 250_000, "NVL"), ("RECY-F", 200_000, "NVL"),
            ("OIL-SP", 3_000, "NVL"),
            ("CONE", 120_000, "NVL"), ("BAG-PP", 60_000, "NVL"), ("LABEL", 200_000, "NVL"),
            ("WRAP-PE", 8_000, "NVL"), ("STRAP", 30_000, "NVL"),
            ("CVCM-30", 20_000, "TP"), ("CVCD-30", 12_000, "TP"), ("TCCM-30", 18_000, "TP"),
            ("TCCD-30", 15_000, "TP"), ("RECY-30", 10_000, "TP"),
            ("CVCM-26", 8_000, "TP"), ("TCCM-26", 6_000, "TP"), ("RECY-26", 5_000, "TP")
        };

        foreach (var (code, qty, warehouseCode) in opening)
        {
            var item = _items[code];
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionDate = openingDate,
                ItemId = item.Id,
                WarehouseId = _warehouses[warehouseCode].Id,
                Type = InventoryTransactionType.Opening,
                Quantity = qty,
                UnitCost = item.StandardCost,
                Amount = qty * item.StandardCost,
                ReferenceType = ReferenceType.Opening,
                ReferenceNo = "SỐ DƯ ĐẦU KỲ",
                Note = "Số dư đầu kỳ",
                CreatedById = _admin.Id,
                CreatedAt = openingDate.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Utc)
            });
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedInventoryDocumentsAsync()
    {
        // Nhập xơ hàng tháng (đã ghi sổ) — đủ nguyên liệu cho 12 tháng sản xuất.
        for (var m = -11; m <= -1; m++)
        {
            var receiptDate = MonthStart(m).AddDays(4);

            await AddStockDocumentAsync(StockDocumentType.Receipt, receiptDate,
                "NVL", null, "COT", new (string ItemCode, decimal Qty, string? Note)[]
                {
                    ("COT-US", 220_000m, "Nhập xơ cotton Mỹ"),
                    ("COT-BR", 110_000m, "Nhập xơ cotton Brazil"),
                    ("COT-VN", 340_000m, "Nhập xơ cotton Việt Nam")
                }, DocumentStatus.Posted, _purchasing, _purchasing, _manager, _warehouse,
                "Nhập xơ cotton tháng " + receiptDate.ToString("MM/yyyy"));

            await AddStockDocumentAsync(StockDocumentType.Receipt, receiptDate.AddDays(1),
                "NVL", null, "PES", new (string ItemCode, decimal Qty, string? Note)[]
                {
                    ("PES-14", 145_000m, null),
                    ("PES-12", 120_000m, null),
                    ("RECY-F", 180_000m, null),
                    ("OIL-SP", 9_000m, "Dầu bôi trơn cọc sợi")
                }, DocumentStatus.Posted, _purchasing, _purchasing, _manager, _warehouse,
                "Nhập xơ polyester, xơ tái chế và dầu bôi trơn tháng " + receiptDate.ToString("MM/yyyy"));
        }

        // Nhập bao bì theo quý.
        for (var m = -11; m <= -1; m += 3)
        {
            await AddStockDocumentAsync(StockDocumentType.Receipt, MonthStart(m).AddDays(6),
                "NVL", null, "PK", new (string ItemCode, decimal Qty, string? Note)[]
                {
                    ("CONE", 120_000m, null), ("BAG-PP", 60_000m, null), ("LABEL", 200_000m, null),
                    ("WRAP-PE", 4_000m, null), ("STRAP", 15_000m, null)
                }, DocumentStatus.Posted, _purchasing, _purchasing, _manager, _warehouse,
                "Nhập bao bì quý " + MonthStart(m).ToString("MM/yyyy"));
        }

        // Phiếu nhập đang chờ duyệt.
        await AddStockDocumentAsync(StockDocumentType.Receipt, _today.AddDays(-1),
            "NVL", null, "PK", new (string ItemCode, decimal Qty, string? Note)[]
            {
                ("BAG-PP", 20_000m, null), ("WRAP-PE", 2_000m, null), ("LABEL", 50_000m, null)
            }, DocumentStatus.Submitted, _purchasing, _purchasing, null, null,
            "Đề nghị nhập bao bì tháng này");

        // Điều chuyển bao bì từ kho nguyên liệu sang kho phụ liệu để đóng kiện.
        await AddStockDocumentAsync(StockDocumentType.Transfer, MonthStart(-2).AddDays(20),
            "NVL", "PK", null, new (string ItemCode, decimal Qty, string? Note)[]
            {
                ("CONE", 20_000m, null), ("BAG-PP", 10_000m, null), ("LABEL", 30_000m, null),
                ("WRAP-PE", 4_000m, null), ("STRAP", 10_000m, null)
            }, DocumentStatus.Posted, _warehouse, _warehouse, _manager, _warehouse,
            "Điều chuyển bao bì sang kho phụ liệu");

        // Xuất vật tư đóng gói đã ghi sổ.
        await AddStockDocumentAsync(StockDocumentType.Issue, _today.AddDays(-3),
            "PK", null, null, new (string ItemCode, decimal Qty, string? Note)[]
            {
                ("WRAP-PE", 300m, "Xuất bọc hàng mẫu"), ("STRAP", 1_000m, "Xuất đóng kiện mẫu")
            }, DocumentStatus.Posted, _warehouse, _warehouse, _manager, _warehouse,
            "Xuất vật tư đóng gói cho xưởng mẫu");

        // Phiếu xuất đã duyệt, chờ thủ kho ghi sổ.
        await AddStockDocumentAsync(StockDocumentType.Issue, _today.AddDays(-2),
            "NVL", null, null, new (string ItemCode, decimal Qty, string? Note)[]
            {
                ("OIL-SP", 50m, "Xuất bảo trì cọc sợi")
            }, DocumentStatus.Approved, _warehouse, _warehouse, _manager, null,
            "Xuất dầu bôi trơn bảo trì thiết bị");
    }

    private async Task<StockDocument> AddStockDocumentAsync(StockDocumentType type, DateOnly date,
        string warehouseCode, string? destinationWarehouseCode, string? supplierCode,
        IEnumerable<(string ItemCode, decimal Qty, string? Note)> lines, DocumentStatus status,
        ApplicationUser creator, ApplicationUser? submitter, ApplicationUser? approver, ApplicationUser? poster,
        string? note)
    {
        var document = new StockDocument
        {
            DocumentNo = NextNo(type switch
            {
                StockDocumentType.Receipt => DocumentPrefixes.Receipt,
                StockDocumentType.Issue => DocumentPrefixes.Issue,
                _ => DocumentPrefixes.Transfer
            }, date),
            Type = type,
            Status = status,
            DocumentDate = date,
            WarehouseId = _warehouses[warehouseCode].Id,
            DestinationWarehouseId = destinationWarehouseCode is null ? null : _warehouses[destinationWarehouseCode].Id,
            SupplierId = supplierCode is null ? null : _suppliers[supplierCode].Id,
            Note = note,
            CreatedById = creator.Id,
            CreatedAt = Stamp(date, 7)
        };

        foreach (var (itemCode, qty, lineNote) in lines)
        {
            var item = _items[itemCode];
            document.Lines.Add(new StockDocumentLine
            {
                ItemId = item.Id,
                Quantity = qty,
                UnitCost = item.StandardCost,
                Amount = qty * item.StandardCost,
                Note = lineNote
            });
        }

        document.TotalAmount = document.Lines.Sum(x => x.Amount);

        if (status is DocumentStatus.Submitted or DocumentStatus.Approved or DocumentStatus.Posted)
        {
            document.SubmittedById = submitter?.Id;
            document.SubmittedAt = Stamp(date, 8);
        }

        if (status is DocumentStatus.Approved or DocumentStatus.Posted)
        {
            document.ApprovedById = approver?.Id;
            document.ApprovedAt = Stamp(date, 9);
        }

        if (status == DocumentStatus.Posted)
        {
            document.PostedById = poster?.Id;
            document.PostedAt = Stamp(date, 10);
        }

        _db.StockDocuments.Add(document);
        await _db.SaveChangesAsync();

        if (status != DocumentStatus.Posted)
        {
            return document;
        }

        foreach (var line in document.Lines)
        {
            if (type == StockDocumentType.Receipt)
            {
                AddLedgerTransaction(date, line.ItemId, document.WarehouseId, InventoryTransactionType.GoodsReceipt,
                    line.Quantity, line.UnitCost, ReferenceType.StockDocument, document.Id, document.DocumentNo,
                    note, poster!.Id);
            }
            else if (type == StockDocumentType.Issue)
            {
                AddLedgerTransaction(date, line.ItemId, document.WarehouseId, InventoryTransactionType.GoodsIssue,
                    -line.Quantity, line.UnitCost, ReferenceType.StockDocument, document.Id, document.DocumentNo,
                    note, poster!.Id);
            }
            else
            {
                AddLedgerTransaction(date, line.ItemId, document.WarehouseId, InventoryTransactionType.TransferOut,
                    -line.Quantity, line.UnitCost, ReferenceType.StockDocument, document.Id, document.DocumentNo,
                    note, poster!.Id);
                AddLedgerTransaction(date, line.ItemId, document.DestinationWarehouseId!.Value,
                    InventoryTransactionType.TransferIn, line.Quantity, line.UnitCost,
                    ReferenceType.StockDocument, document.Id, document.DocumentNo, note, poster!.Id);
            }
        }

        await _db.SaveChangesAsync();
        return document;
    }

    private async Task SeedProductionAsync()
    {
        var definitions = new List<OrderSeed>();
        var products = new[] { "CVCM-30", "CVCD-30", "TCCM-30", "TCCD-30", "RECY-30" };

        // 12 tháng × 5 dòng sợi; tháng hiện tại có đủ trạng thái để demo quy trình duyệt.
        for (var month = -11; month <= 0; month++)
        {
            for (var p = 0; p < products.Length; p++)
            {
                var status = month < 0
                    ? ProductionOrderStatus.Completed
                    : p switch
                    {
                        0 => ProductionOrderStatus.Completed,
                        1 => ProductionOrderStatus.InProgress,
                        2 => ProductionOrderStatus.Approved,
                        3 => ProductionOrderStatus.Submitted,
                        _ => ProductionOrderStatus.Draft
                    };

                var dueDate = month < 0
                    ? MonthDay(month, 20)
                    : _today.AddDays(p switch { 0 => -6, 1 => -2, 2 => 5, 3 => 10, _ => 15 });

                var plannedQuantity = month < 0
                    ? 201_400m
                    : 180_000m + p * 10_000m;

                definitions.Add(new OrderSeed(products[p], plannedQuantity, dueDate, status));
            }
        }

        var index = 0;
        foreach (var definition in definitions)
        {
            index++;
            var product = _items[definition.ProductCode];
            var bom = _boms[definition.ProductCode];
            var startDate = definition.DueDate.AddDays(-12);
            var order = new ProductionOrder
            {
                OrderNo = NextNo(DocumentPrefixes.ProductionOrder, definition.DueDate),
                FinishedGoodItemId = product.Id,
                BomId = bom.Id,
                OutputWarehouseId = _warehouses["TP"].Id,
                PlannedQuantity = definition.PlannedQuantity,
                StartDate = startDate,
                DueDate = definition.DueDate,
                Status = definition.Status,
                Note = $"Lệnh sản xuất {product.Name} theo BOM {bom.Code}",
                CreatedById = _production.Id,
                CreatedAt = Stamp(startDate, 8)
            };

            if (definition.Status is ProductionOrderStatus.Submitted or ProductionOrderStatus.Approved
                or ProductionOrderStatus.InProgress or ProductionOrderStatus.Completed)
            {
                order.SubmittedById = _production.Id;
                order.SubmittedAt = Stamp(startDate, 10);
            }

            if (definition.Status is ProductionOrderStatus.Approved or ProductionOrderStatus.InProgress
                or ProductionOrderStatus.Completed)
            {
                order.ApprovedById = _manager.Id;
                order.ApprovedAt = Stamp(startDate.AddDays(1), 9);
            }

            foreach (var line in bom.Lines)
            {
                var component = _items.Values.First(x => x.Id == line.ComponentItemId);
                var standardQty = decimal.Round(
                    definition.PlannedQuantity * line.QuantityPerUnit * (1 + line.ScrapPercent / 100m), 4,
                    MidpointRounding.AwayFromZero);
                var standardAmount = decimal.Round(standardQty * component.StandardCost, 4,
                    MidpointRounding.AwayFromZero);

                order.Materials.Add(new ProductionOrderMaterial
                {
                    ItemId = component.Id,
                    StandardQuantity = standardQty,
                    UnitCost = component.StandardCost,
                    StandardAmount = standardAmount
                });
            }

            order.StandardMaterialCost = order.Materials.Sum(x => x.StandardAmount);
            _db.ProductionOrders.Add(order);
            await _db.SaveChangesAsync();

            if (definition.Status is ProductionOrderStatus.InProgress or ProductionOrderStatus.Completed)
            {
                var issueDate = definition.Status == ProductionOrderStatus.Completed
                    ? definition.DueDate.AddDays(-8)
                    : _today.AddDays(-3);
                if (issueDate > _today)
                {
                    issueDate = _today.AddDays(-1);
                }

                var factorBase = definition.Status == ProductionOrderStatus.Completed ? 1m : 0.55m;
                var issueLines = new List<(Item Item, decimal Quantity, decimal UnitCost, string? Note)>();

                foreach (var material in order.Materials)
                {
                    var component = _items.Values.First(x => x.Id == material.ItemId);
                    decimal factor;
                    if (definition.Status == ProductionOrderStatus.Completed)
                    {
                        factor = 0.92m + (decimal)_random.NextDouble() * 0.25m;
                        if (index is 2 or 5)
                        {
                            factor += 0.04m;
                        }
                    }
                    else
                    {
                        factor = factorBase;
                    }

                    var qty = decimal.Round(material.StandardQuantity * factor, 4, MidpointRounding.AwayFromZero);
                    if (qty <= 0)
                    {
                        continue;
                    }

                    issueLines.Add((component, qty, component.StandardCost, null));
                }

                var postedCost = await AddMaterialIssueAsync(order, issueDate, issueLines, DocumentStatus.Posted,
                    $"Cấp phát vật tư cho {order.OrderNo}");

                if (definition.Status == ProductionOrderStatus.Completed)
                {
                    var goodQty = decimal.Round(definition.PlannedQuantity * (0.94m + (decimal)_random.NextDouble() * 0.07m),
                        0, MidpointRounding.AwayFromZero);
                    var defectQty = decimal.Round(goodQty * (0.01m + (decimal)_random.NextDouble() * 0.03m),
                        0, MidpointRounding.AwayFromZero);
                    if (goodQty <= 0)
                    {
                        goodQty = definition.PlannedQuantity;
                    }

                    var unitCost = goodQty > 0 ? decimal.Round(postedCost / goodQty, 4) : 0;
                    var outputDate = definition.DueDate > _today ? _today : definition.DueDate;
                    _db.ProductionOutputs.Add(new ProductionOutput
                    {
                        ProductionOrderId = order.Id,
                        OutputDate = outputDate,
                        GoodQuantity = goodQty,
                        DefectQuantity = defectQty,
                        UnitCost = unitCost,
                        TotalCost = postedCost,
                        Note = "Nhập thành phẩm hoàn thành lệnh sản xuất",
                        CreatedById = _production.Id,
                        CreatedAt = Stamp(outputDate, 16)
                    });

                    AddLedgerTransaction(outputDate, product.Id, _warehouses["TP"].Id,
                        InventoryTransactionType.ProductionReceipt, goodQty, unitCost,
                        ReferenceType.ProductionOrder, order.Id, order.OrderNo,
                        "Nhập thành phẩm từ lệnh sản xuất", _production.Id, postedCost);

                    order.ActualQuantity = goodQty;
                    order.DefectQuantity = defectQty;
                    order.ActualMaterialCost = postedCost;
                    order.Status = ProductionOrderStatus.Completed;
                    order.CompletedAt = Stamp(outputDate, 16);
                    await _db.SaveChangesAsync();
                }
            }
            else if (definition.Status == ProductionOrderStatus.Approved)
            {
                var issueLines = order.Materials.Select(material =>
                {
                    var component = _items.Values.First(x => x.Id == material.ItemId);
                    return (component, material.StandardQuantity, material.UnitCost, (string?)null);
                }).ToList();

                await AddMaterialIssueAsync(order, _today.AddDays(-1), issueLines, DocumentStatus.Submitted,
                    "Trình duyệt cấp phát vật tư");
            }
        }
    }

    private async Task<decimal> AddMaterialIssueAsync(ProductionOrder order, DateOnly date,
        List<(Item Item, decimal Quantity, decimal UnitCost, string? Note)> lineData, DocumentStatus status,
        string note)
    {
        var issue = new MaterialIssue
        {
            IssueNo = NextNo(DocumentPrefixes.MaterialIssue, date),
            ProductionOrderId = order.Id,
            WarehouseId = _warehouses["NVL"].Id,
            IssueDate = date,
            Status = status,
            Note = note,
            CreatedById = _production.Id,
            CreatedAt = Stamp(date, 8)
        };

        foreach (var (item, quantity, unitCost, lineNote) in lineData)
        {
            issue.Lines.Add(new MaterialIssueLine
            {
                ItemId = item.Id,
                Quantity = quantity,
                UnitCost = unitCost,
                Amount = decimal.Round(quantity * unitCost, 4),
                Note = lineNote
            });
        }

        issue.TotalAmount = issue.Lines.Sum(x => x.Amount);

        if (status is DocumentStatus.Submitted or DocumentStatus.Approved or DocumentStatus.Posted)
        {
            issue.SubmittedById = _production.Id;
            issue.SubmittedAt = Stamp(date, 9);
        }

        if (status is DocumentStatus.Approved or DocumentStatus.Posted)
        {
            issue.ApprovedById = _manager.Id;
            issue.ApprovedAt = Stamp(date, 10);
        }

        if (status == DocumentStatus.Posted)
        {
            issue.PostedById = _warehouse.Id;
            issue.PostedAt = Stamp(date, 11);
        }

        _db.MaterialIssues.Add(issue);
        await _db.SaveChangesAsync();

        if (status != DocumentStatus.Posted)
        {
            return 0;
        }

        foreach (var line in issue.Lines)
        {
            AddLedgerTransaction(date, line.ItemId, issue.WarehouseId, InventoryTransactionType.ProductionIssue,
                -line.Quantity, line.UnitCost, ReferenceType.MaterialIssue, issue.Id, issue.IssueNo,
                note, _warehouse.Id);
        }

        order.ActualMaterialCost += issue.TotalAmount;
        if (order.Status == ProductionOrderStatus.Approved)
        {
            order.Status = ProductionOrderStatus.InProgress;
        }

        await _db.SaveChangesAsync();
        return issue.TotalAmount;
    }

    private void AddLedgerTransaction(DateOnly date, int itemId, int warehouseId, InventoryTransactionType type,
        decimal quantity, decimal unitCost, ReferenceType referenceType, int referenceId, string referenceNo,
        string? note, string userId, decimal? amountOverride = null)
    {
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionDate = date,
            ItemId = itemId,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = quantity,
            UnitCost = unitCost,
            Amount = amountOverride ?? decimal.Round(quantity * unitCost, 4),
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ReferenceNo = referenceNo,
            Note = note,
            CreatedById = userId,
            CreatedAt = Stamp(date, 12)
        });
    }

    private async Task SeedNumberSequencesAsync()
    {
        foreach (var ((prefix, year), last) in _sequences)
        {
            _db.NumberSequences.Add(new NumberSequence { Prefix = prefix, Year = year, LastNumber = last });
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedAuditLogsAsync()
    {
        var logs = new[]
        {
            new AuditLog { Timestamp = DateTime.UtcNow.AddDays(-1).AddHours(-3), UserId = _purchasing.Id, UserName = "muahang", Action = "SUBMIT", EntityName = "StockDocument", EntityId = "PN-2026-0005", Description = "Trình duyệt phiếu nhập bao bì tháng này", IpAddress = "127.0.0.1" },
            new AuditLog { Timestamp = DateTime.UtcNow.AddDays(-1).AddHours(-1), UserId = _production.Id, UserName = "sanxuat", Action = "SUBMIT", EntityName = "MaterialIssue", EntityId = "CCP-2026-0009", Description = "Trình duyệt cấp phát vật tư", IpAddress = "127.0.0.1" },
            new AuditLog { Timestamp = DateTime.UtcNow.AddHours(-5), UserId = _warehouse.Id, UserName = "kho", Action = "POST", EntityName = "StockDocument", EntityId = "PN-2026-0004", Description = "Ghi sổ phiếu nhập vật tư lắp ráp", IpAddress = "127.0.0.1" },
            new AuditLog { Timestamp = DateTime.UtcNow.AddHours(-4), UserId = _manager.Id, UserName = "manager", Action = "APPROVE", EntityName = "ProductionOrder", EntityId = "LSX-2026-0008", Description = "Phê duyệt lệnh sản xuất sợi TCCM Ne 30", IpAddress = "127.0.0.1" },
            new AuditLog { Timestamp = DateTime.UtcNow.AddHours(-2), UserId = _warehouse.Id, UserName = "kho", Action = "POST", EntityName = "MaterialIssue", EntityId = "CCP-2026-0007", Description = "Ghi sổ cấp phát vật tư cho LSX-2026-0007", IpAddress = "127.0.0.1" },
            new AuditLog { Timestamp = DateTime.UtcNow.AddMinutes(-40), UserId = _production.Id, UserName = "sanxuat", Action = "COMPLETE", EntityName = "ProductionOrder", EntityId = "LSX-2026-0006", Description = "Hoàn thành lệnh sản xuất sợi CVCM Ne 30, nhập 190.000 kg", IpAddress = "127.0.0.1" },
            new AuditLog { Timestamp = DateTime.UtcNow.AddMinutes(-15), UserId = _admin.Id, UserName = "admin", Action = "LOGIN", EntityName = "Account", EntityId = _admin.Id, Description = "Đăng nhập hệ thống", IpAddress = "127.0.0.1" }
        };

        _db.AuditLogs.AddRange(logs);
        await _db.SaveChangesAsync();
    }

    private string NextNo(string prefix, DateOnly date)
    {
        var key = (prefix, date.Year);
        _sequences.TryGetValue(key, out var last);
        last++;
        _sequences[key] = last;
        return $"{prefix}-{date.Year}-{last:D4}";
    }

    private DateOnly MonthStart(int monthsFromCurrent)
    {
        var month = new DateOnly(_today.Year, _today.Month, 1).AddMonths(monthsFromCurrent);
        return month;
    }

    private DateOnly MonthDay(int monthsFromCurrent, int day)
    {
        var month = MonthStart(monthsFromCurrent);
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        return month.AddDays(Math.Min(day, days) - 1);
    }

    private static DateTime Stamp(DateOnly date, int hour)
        => date.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);

    private sealed record OrderSeed(string ProductCode, decimal PlannedQuantity, DateOnly DueDate,
        ProductionOrderStatus Status);
}
