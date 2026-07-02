using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class WarehouseIssuanceConfiguration : IEntityTypeConfiguration<WarehouseIssuance>
{
    public void Configure(EntityTypeBuilder<WarehouseIssuance> builder)
    {
            builder
                            .HasIndex(issuance => issuance.IssuanceNumber)
                            .IsUnique();

            builder
                            .Property(issuance => issuance.IssuanceNumber)
                            .HasMaxLength(40);

            builder
                            .Property(issuance => issuance.DateShamsi)
                            .HasMaxLength(20);

            builder
                            .Property(issuance => issuance.DestinationOrDepartment)
                            .HasMaxLength(200);

            builder
                            .Property(issuance => issuance.Notes)
                            .HasMaxLength(600);

            builder
                            .Property(issuance => issuance.WorkflowStatus)
                            .HasMaxLength(30)
                            .HasDefaultValue(WorkflowStatus.Approved);

            builder
                            .HasOne(issuance => issuance.Warehouse)
                            .WithMany(warehouse => warehouse.Issuances)
                            .HasForeignKey(issuance => issuance.WarehouseId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(issuance => issuance.Employer)
                            .WithMany(employer => employer.Issuances)
                            .HasForeignKey(issuance => issuance.EmployerId)
                            .OnDelete(DeleteBehavior.SetNull);
    }
}
