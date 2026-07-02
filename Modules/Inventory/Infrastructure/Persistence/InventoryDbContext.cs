using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;

public sealed class InventoryDbContext : ModularDbContext
{
    public InventoryDbContext(
        DbContextOptions<InventoryDbContext> options,
        ITenantIsolationService? tenantIsolationService = null)
        : base(options, tenantIsolationService)
    {
    }

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<WarehouseReceipt> WarehouseReceipts => Set<WarehouseReceipt>();
    public DbSet<WarehouseReceiptItem> WarehouseReceiptItems => Set<WarehouseReceiptItem>();
    public DbSet<WarehouseIssuance> WarehouseIssuances => Set<WarehouseIssuance>();
    public DbSet<WarehouseIssuanceItem> WarehouseIssuanceItems => Set<WarehouseIssuanceItem>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<InventoryCounting> InventoryCountings => Set<InventoryCounting>();
    public DbSet<InventoryCountingItem> InventoryCountingItems => Set<InventoryCountingItem>();
    public DbSet<WarehouseClosing> WarehouseClosings => Set<WarehouseClosing>();
    public DbSet<WarehouseClosingItem> WarehouseClosingItems => Set<WarehouseClosingItem>();
    public DbSet<InventoryOpeningBalanceLedger> InventoryOpeningBalanceLedgers => Set<InventoryOpeningBalanceLedger>();
    public DbSet<InventoryMovementLedger> InventoryMovementLedgers => Set<InventoryMovementLedger>();
    public DbSet<InventoryTransferRequest> InventoryTransferRequests => Set<InventoryTransferRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyTenantSchema(modelBuilder);

        modelBuilder.Ignore<IdentityUserRole<string>>();
        modelBuilder.Ignore<IdentityUserClaim<string>>();
        modelBuilder.Ignore<IdentityUserLogin<string>>();
        modelBuilder.Ignore<IdentityUserToken<string>>();
        modelBuilder.Ignore<IdentityRoleClaim<string>>();
        modelBuilder.Ignore<ApplicationRole>();
        modelBuilder.Ignore<Department>();

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("AspNetUsers");
            builder.Ignore(item => item.Department);
            builder.Ignore(item => item.Manager);
            builder.Ignore(item => item.ParentManagerUser);
            builder.Ignore(item => item.Employee);
        });
        modelBuilder.Entity<Vendor>().ToTable("Vendors");
        modelBuilder.Entity<Employer>().ToTable("Employers");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InventoryDbContext).Assembly,
            type => type.Namespace?.StartsWith("OfficeAutomation.Modules.Inventory.", StringComparison.Ordinal) == true);
    }
}
