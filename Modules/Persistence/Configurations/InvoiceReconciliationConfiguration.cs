using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Persistence.Configurations;

public sealed class InvoiceReconciliationResultConfiguration : IEntityTypeConfiguration<InvoiceReconciliationResult>
{
    public void Configure(EntityTypeBuilder<InvoiceReconciliationResult> builder)
    {
        builder.Property(r => r.TaxReferenceId).HasMaxLength(100);
        builder.Property(r => r.PortalTotalAmount).HasPrecision(18, 2);
        builder.Property(r => r.PortalVatAmount).HasPrecision(18, 2);
        builder.Property(r => r.PortalVatRate).HasPrecision(5, 4);
        builder.Property(r => r.PortalSellerEconomicCode).HasMaxLength(50);
        builder.Property(r => r.PortalBuyerEconomicCode).HasMaxLength(50);

        builder.HasOne(r => r.InvoiceRecord)
            .WithMany(i => i.ReconciliationResults)
            .HasForeignKey(r => r.InvoiceRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Submission)
            .WithMany()
            .HasForeignKey(r => r.SubmissionId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasMany(r => r.Discrepancies)
            .WithOne(d => d.ReconciliationResult)
            .HasForeignKey(d => d.ReconciliationResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.InvoiceRecordId).HasDatabaseName("IX_ReconciliationResult_InvoiceRecordId");
        builder.HasIndex(r => r.Status).HasDatabaseName("IX_ReconciliationResult_Status");
    }
}

public sealed class ReconciliationDiscrepancyConfiguration : IEntityTypeConfiguration<ReconciliationDiscrepancy>
{
    public void Configure(EntityTypeBuilder<ReconciliationDiscrepancy> builder)
    {
        builder.Property(d => d.Type).HasMaxLength(60);
        builder.Property(d => d.LocalValue).HasMaxLength(500);
        builder.Property(d => d.PortalValue).HasMaxLength(500);
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.Suggestion).HasMaxLength(1000);

        builder.HasIndex(d => d.ReconciliationResultId).HasDatabaseName("IX_Discrepancy_ReconciliationResultId");
    }
}