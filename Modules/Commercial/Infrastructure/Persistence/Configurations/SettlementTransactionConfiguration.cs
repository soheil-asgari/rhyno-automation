using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Commercial.Domain;

namespace OfficeAutomation.Modules.Commercial.Infrastructure.Persistence.Configurations;

public sealed class SettlementTransactionConfiguration : IEntityTypeConfiguration<SettlementTransaction>
{
    public void Configure(EntityTypeBuilder<SettlementTransaction> builder)
    {
        builder.HasIndex(s => new { s.InvoiceId, s.SettlementDate }).HasDatabaseName("IX_Settlement_Invoice_Date");

        builder.Property(s => s.SettlementType).HasMaxLength(20).IsRequired();
        builder.Property(s => s.ReferenceNumber).HasMaxLength(50);
        builder.Property(s => s.DateShamsi).HasMaxLength(20);
        builder.Property(s => s.PaymentMethod).HasMaxLength(50);
        builder.Property(s => s.Notes).HasMaxLength(300);
        builder.Property(s => s.Status).HasMaxLength(30);
        builder.Property(s => s.VoucherDocumentNumber).HasMaxLength(50);
        builder.Property(s => s.CreatedByUserId).HasMaxLength(450);

        builder.Property(s => s.Amount).HasPrecision(18, 2);

        builder.HasOne(s => s.Invoice)
            .WithMany()
            .HasForeignKey(s => s.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}