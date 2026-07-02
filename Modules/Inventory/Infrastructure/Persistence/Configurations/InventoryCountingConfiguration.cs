using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryCountingConfiguration : IEntityTypeConfiguration<InventoryCounting>
{
    public void Configure(EntityTypeBuilder<InventoryCounting> builder)
    {
            builder
                            .HasIndex(counting => counting.DocumentNumber)
                            .IsUnique();

            builder
                            .Property(counting => counting.DocumentNumber)
                            .HasMaxLength(40);

            builder
                            .Property(counting => counting.DateShamsi)
                            .HasMaxLength(20);

            builder
                            .Property(counting => counting.Status)
                            .HasMaxLength(20);

            builder
                            .Property(counting => counting.Notes)
                            .HasMaxLength(600);

            builder
                            .HasOne(counting => counting.Warehouse)
                            .WithMany(warehouse => warehouse.Countings)
                            .HasForeignKey(counting => counting.WarehouseId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
