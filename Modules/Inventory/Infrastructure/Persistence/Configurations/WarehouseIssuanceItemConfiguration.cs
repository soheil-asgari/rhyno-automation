using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class WarehouseIssuanceItemConfiguration : IEntityTypeConfiguration<WarehouseIssuanceItem>
{
    public void Configure(EntityTypeBuilder<WarehouseIssuanceItem> builder)
    {
            builder
                            .Property(item => item.Quantity)
                            .HasPrecision(18, 3);

            builder
                            .HasOne(item => item.WarehouseIssuance)
                            .WithMany(issuance => issuance.Items)
                            .HasForeignKey(item => item.WarehouseIssuanceId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.Product)
                            .WithMany(product => product.IssuanceItems)
                            .HasForeignKey(item => item.ProductId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
