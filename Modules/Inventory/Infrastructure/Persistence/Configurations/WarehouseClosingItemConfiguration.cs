using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class WarehouseClosingItemConfiguration : IEntityTypeConfiguration<WarehouseClosingItem>
{
    public void Configure(EntityTypeBuilder<WarehouseClosingItem> builder)
    {
            builder
                            .Property(item => item.ClosingQuantity)
                            .HasPrecision(18, 3);

            builder
                            .Property(item => item.OpeningQuantity)
                            .HasPrecision(18, 3);

            builder
                            .HasOne(item => item.WarehouseClosing)
                            .WithMany(closing => closing.Items)
                            .HasForeignKey(item => item.WarehouseClosingId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.Product)
                            .WithMany()
                            .HasForeignKey(item => item.ProductId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
