using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Persistence.Configurations;

public sealed class InvoiceRecordConfiguration : IEntityTypeConfiguration<InvoiceRecord>
{
    public void Configure(EntityTypeBuilder<InvoiceRecord> builder)
    {
        builder.Property(r => r.InvoiceNumber).HasMaxLength(100).IsRequired();
        builder.Property(r => r.DateShamsi).HasMaxLength(20).IsRequired();
        builder.Property(r => r.TotalAmount).HasPrecision(18, 2);
        builder.Property(r => r.SubTotal).HasPrecision(18, 2);
        builder.Property(r => r.DiscountAmount).HasPrecision(18, 2);
        builder.Property(r => r.VatAmount).HasPrecision(18, 2);
        builder.Property(r => r.DutyAmount).HasPrecision(18, 2);
        builder.Property(r => r.FinalAmount).HasPrecision(18, 2);
        builder.Property(r => r.VatRate).HasPrecision(5, 4);
        builder.Property(r => r.InvoiceType).HasMaxLength(50);
        builder.Property(r => r.Status).HasMaxLength(50);
        builder.Property(r => r.Notes).HasMaxLength(500);
        builder.Property(r => r.OwnerKey).HasMaxLength(50);

        builder.HasOne(r => r.Seller)
            .WithMany(s => s.Invoices)
            .HasForeignKey(r => r.SellerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.Buyer)
            .WithMany(b => b.Invoices)
            .HasForeignKey(r => r.BuyerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(r => r.LineItems)
            .WithOne(li => li.InvoiceRecord)
            .HasForeignKey(li => li.InvoiceRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.TaxLogs)
            .WithOne(tl => tl.InvoiceRecord)
            .HasForeignKey(tl => tl.InvoiceRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
