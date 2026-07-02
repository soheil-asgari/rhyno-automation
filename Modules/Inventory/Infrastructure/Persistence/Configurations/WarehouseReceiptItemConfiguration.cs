using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class WarehouseReceiptItemConfiguration : IEntityTypeConfiguration<WarehouseReceiptItem>
{
    public void Configure(EntityTypeBuilder<WarehouseReceiptItem> builder)
    {
            builder
                            .Property(item => item.Quantity)
                            .HasPrecision(18, 3);

            builder
                            .Property(item => item.UnitPrice)
                            .HasPrecision(18, 2);

            builder
                            .HasOne(item => item.WarehouseReceipt)
                            .WithMany(receipt => receipt.Items)
                            .HasForeignKey(item => item.WarehouseReceiptId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.Product)
                            .WithMany(product => product.ReceiptItems)
                            .HasForeignKey(item => item.ProductId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
