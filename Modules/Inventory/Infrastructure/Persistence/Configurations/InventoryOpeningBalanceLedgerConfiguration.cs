using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryOpeningBalanceLedgerConfiguration : IEntityTypeConfiguration<InventoryOpeningBalanceLedger>
{
    public void Configure(EntityTypeBuilder<InventoryOpeningBalanceLedger> builder)
    {
            builder
                            .HasIndex(item => new { item.WarehouseId, item.ProductId, item.PeriodYear })
                            .IsUnique();

            builder
                            .Property(item => item.Quantity)
                            .HasPrecision(18, 3);

            builder
                            .HasOne(item => item.Warehouse)
                            .WithMany(warehouse => warehouse.OpeningLedgers)
                            .HasForeignKey(item => item.WarehouseId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.Product)
                            .WithMany()
                            .HasForeignKey(item => item.ProductId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.WarehouseClosing)
                            .WithMany()
                            .HasForeignKey(item => item.WarehouseClosingId)
                            .OnDelete(DeleteBehavior.Cascade);
    }
}
