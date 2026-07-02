using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryTransferRequestConfiguration : IEntityTypeConfiguration<InventoryTransferRequest>
{
    public void Configure(EntityTypeBuilder<InventoryTransferRequest> builder)
    {
            builder
                            .Property(item => item.Status)
                            .HasMaxLength(30)
                            .HasDefaultValue(WorkflowStatus.PendingApproval);

            builder
                            .Property(item => item.Quantity)
                            .HasPrecision(18, 3);

            builder
                            .HasOne(item => item.SourceWarehouse)
                            .WithMany()
                            .HasForeignKey(item => item.SourceWarehouseId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.DestinationWarehouse)
                            .WithMany()
                            .HasForeignKey(item => item.DestinationWarehouseId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.Product)
                            .WithMany()
                            .HasForeignKey(item => item.ProductId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.RequestedByUser)
                            .WithMany()
                            .HasForeignKey(item => item.RequestedByUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.ApprovedByUser)
                            .WithMany()
                            .HasForeignKey(item => item.ApprovedByUserId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
