using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class WarehouseReceiptConfiguration : IEntityTypeConfiguration<WarehouseReceipt>
{
    public void Configure(EntityTypeBuilder<WarehouseReceipt> builder)
    {
            builder
                            .HasIndex(receipt => receipt.ReceiptNumber)
                            .IsUnique();

            builder
                            .Property(receipt => receipt.ReceiptNumber)
                            .HasMaxLength(40);

            builder
                            .Property(receipt => receipt.DateShamsi)
                            .HasMaxLength(20);

            builder
                            .Property(receipt => receipt.SupplierOrSource)
                            .HasMaxLength(200);

            builder
                            .Property(receipt => receipt.Notes)
                            .HasMaxLength(600);

            builder
                            .Property(receipt => receipt.WorkflowStatus)
                            .HasMaxLength(30)
                            .HasDefaultValue(WorkflowStatus.Approved);

            builder
                            .HasOne(receipt => receipt.Warehouse)
                            .WithMany(warehouse => warehouse.Receipts)
                            .HasForeignKey(receipt => receipt.WarehouseId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(receipt => receipt.Vendor)
                            .WithMany(vendor => vendor.Receipts)
                            .HasForeignKey(receipt => receipt.VendorId)
                            .OnDelete(DeleteBehavior.SetNull);
    }
}
