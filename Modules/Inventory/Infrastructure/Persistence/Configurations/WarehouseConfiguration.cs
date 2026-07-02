using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
            builder
                            .HasIndex(warehouse => warehouse.Code)
                            .IsUnique();

            builder
                            .Property(warehouse => warehouse.Code)
                            .HasMaxLength(30);

            builder
                            .Property(warehouse => warehouse.Name)
                            .HasMaxLength(120);

            builder
                            .Property(warehouse => warehouse.Location)
                            .HasMaxLength(200);

            builder
                            .Property(warehouse => warehouse.Capacity)
                            .HasPrecision(18, 2);

            builder
                            .HasOne(warehouse => warehouse.ManagerUser)
                            .WithMany()
                            .HasForeignKey(warehouse => warehouse.ManagerUserId)
                            .OnDelete(DeleteBehavior.SetNull);

            builder.HasData(
                            new Warehouse
                            {
                                Id = 1,
                                Code = "WH-MAIN",
                                Name = "انبار مرکزی",
                                Location = "ستاد",
                                IsActive = true,
                                IsClosed = false,
                                CreatedAt = new DateTime(2026, 1, 1)
                            }
                        );
    }
}
