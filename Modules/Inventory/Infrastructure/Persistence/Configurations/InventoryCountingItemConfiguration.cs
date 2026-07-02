using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryCountingItemConfiguration : IEntityTypeConfiguration<InventoryCountingItem>
{
    public void Configure(EntityTypeBuilder<InventoryCountingItem> builder)
    {
            builder
                            .Property(item => item.SystemQuantity)
                            .HasPrecision(18, 3);

            builder
                            .Property(item => item.PhysicalQuantity)
                            .HasPrecision(18, 3);

            builder
                            .Property(item => item.DiscrepancyQuantity)
                            .HasPrecision(18, 3);

            builder
                            .HasOne(item => item.InventoryCounting)
                            .WithMany(counting => counting.Items)
                            .HasForeignKey(item => item.InventoryCountingId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.Product)
                            .WithMany(product => product.CountingItems)
                            .HasForeignKey(item => item.ProductId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
