using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class WarehouseClosingConfiguration : IEntityTypeConfiguration<WarehouseClosing>
{
    public void Configure(EntityTypeBuilder<WarehouseClosing> builder)
    {
            builder
                            .HasIndex(closing => closing.DocumentNumber)
                            .IsUnique();

            builder
                            .Property(closing => closing.DocumentNumber)
                            .HasMaxLength(40);

            builder
                            .Property(closing => closing.ClosingDateShamsi)
                            .HasMaxLength(20);

            builder
                            .HasOne(closing => closing.Warehouse)
                            .WithMany(warehouse => warehouse.Closings)
                            .HasForeignKey(closing => closing.WarehouseId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
