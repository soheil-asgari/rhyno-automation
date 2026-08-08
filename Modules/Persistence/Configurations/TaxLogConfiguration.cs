using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Persistence.Configurations;

public sealed class TaxLogConfiguration : IEntityTypeConfiguration<TaxLog>
{
    public void Configure(EntityTypeBuilder<TaxLog> builder)
    {
        builder.Property(tl => tl.TotalBeforeTax).HasPrecision(18, 2);
        builder.Property(tl => tl.TotalDiscount).HasPrecision(18, 2);
        builder.Property(tl => tl.TaxableAmount).HasPrecision(18, 2);
        builder.Property(tl => tl.ExemptAmount).HasPrecision(18, 2);
        builder.Property(tl => tl.VatRate).HasPrecision(5, 4);
        builder.Property(tl => tl.TaxAmount).HasPrecision(18, 2);
        builder.Property(tl => tl.DutyAmount).HasPrecision(18, 2);
        builder.Property(tl => tl.FinalAmount).HasPrecision(18, 2);
        builder.Property(tl => tl.SummaryPersian).HasMaxLength(1000);
        builder.Property(tl => tl.CalculatedBy).HasMaxLength(50);

        builder.HasIndex(tl => tl.InvoiceRecordId).HasDatabaseName("IX_TaxLog_InvoiceRecordId");
        builder.HasIndex(tl => tl.CalculatedAt).HasDatabaseName("IX_TaxLog_CalculatedAt");
    }
}
