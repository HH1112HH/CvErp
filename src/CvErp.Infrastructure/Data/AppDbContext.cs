using CvErp.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CvErp.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockDocumentLine> StockDocumentLines => Set<StockDocumentLine>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Bom> Boms => Set<Bom>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionOrderMaterial> ProductionOrderMaterials => Set<ProductionOrderMaterial>();
    public DbSet<MaterialIssue> MaterialIssues => Set<MaterialIssue>();
    public DbSet<MaterialIssueLine> MaterialIssueLines => Set<MaterialIssueLine>();
    public DbSet<ProductionOutput> ProductionOutputs => Set<ProductionOutput>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Item>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.Category).HasMaxLength(100);
        });

        builder.Entity<Warehouse>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Location).HasMaxLength(250);
        });

        builder.Entity<Supplier>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.TaxCode).HasMaxLength(30);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Address).HasMaxLength(300);
        });

        builder.Entity<StockDocument>(e =>
        {
            e.HasIndex(x => x.DocumentNo).IsUnique();
            e.HasIndex(x => new { x.Type, x.Status, x.DocumentDate });
            e.Property(x => x.DocumentNo).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();

            e.HasOne(x => x.Warehouse).WithMany()
                .HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DestinationWarehouse).WithMany()
                .HasForeignKey(x => x.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany()
                .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne(x => x.StockDocument!)
                .HasForeignKey(x => x.StockDocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StockDocumentLine>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.Item).WithMany()
                .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryTransaction>(e =>
        {
            e.HasIndex(x => new { x.ItemId, x.WarehouseId, x.TransactionDate });
            e.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
            e.Property(x => x.ReferenceNo).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(400);
            e.Property(x => x.CreatedById).HasMaxLength(450);

            e.HasOne(x => x.Item).WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Warehouse).WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Bom>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Version).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);

            e.HasOne(x => x.FinishedGoodItem).WithMany()
                .HasForeignKey(x => x.FinishedGoodItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne(x => x.Bom!)
                .HasForeignKey(x => x.BomId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BomLine>(e =>
        {
            e.HasIndex(x => new { x.BomId, x.ComponentItemId }).IsUnique();
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.ComponentItem).WithMany(x => x.BomLines)
                .HasForeignKey(x => x.ComponentItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductionOrder>(e =>
        {
            e.HasIndex(x => x.OrderNo).IsUnique();
            e.HasIndex(x => new { x.Status, x.DueDate });
            e.Property(x => x.OrderNo).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();

            e.HasOne(x => x.FinishedGoodItem).WithMany()
                .HasForeignKey(x => x.FinishedGoodItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Bom).WithMany()
                .HasForeignKey(x => x.BomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.OutputWarehouse).WithMany()
                .HasForeignKey(x => x.OutputWarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Materials).WithOne(x => x.ProductionOrder!)
                .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.MaterialIssues).WithOne(x => x.ProductionOrder!)
                .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Outputs).WithOne(x => x.ProductionOrder!)
                .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductionOrderMaterial>(e =>
        {
            e.HasIndex(x => new { x.ProductionOrderId, x.ItemId }).IsUnique();
            e.HasOne(x => x.Item).WithMany()
                .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MaterialIssue>(e =>
        {
            e.HasIndex(x => x.IssueNo).IsUnique();
            e.HasIndex(x => new { x.Status, x.IssueDate });
            e.Property(x => x.IssueNo).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();

            e.HasOne(x => x.Warehouse).WithMany()
                .HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne(x => x.MaterialIssue!)
                .HasForeignKey(x => x.MaterialIssueId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MaterialIssueLine>(e =>
        {
            e.HasOne(x => x.Item).WithMany()
                .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Note).HasMaxLength(300);
        });

        builder.Entity<ProductionOutput>(e =>
        {
            e.HasIndex(x => x.ProductionOrderId);
            e.Property(x => x.Note).HasMaxLength(400);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => new { x.EntityName, x.EntityId });
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.EntityName).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.IpAddress).HasMaxLength(64);
        });

        builder.Entity<NumberSequence>(e =>
        {
            e.HasIndex(x => new { x.Prefix, x.Year }).IsUnique();
            e.Property(x => x.Prefix).HasMaxLength(10);
        });

        // Tất cả số lượng/tiền tệ dùng DECIMAL(18,4), không dùng float.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties()
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetPrecision(18);
                property.SetScale(4);
            }
        }
    }
}
