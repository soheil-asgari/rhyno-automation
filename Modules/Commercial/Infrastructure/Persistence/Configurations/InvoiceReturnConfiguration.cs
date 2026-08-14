using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Commercial.Domain;

namespace OfficeAutomation.Modules.Commercial.Infrastructure.Persistence.Configurations;

public sealed class InvoiceReturnConfiguration : IEntityTypeConfiguration<InvoiceReturn>
{
    public void Configure(EntityTypeBuilder<InvoiceReturn> builder)
    {
        builder.HasIndex(r => new { r.ReturnType, r.ReturnNumber }).IsUnique().HasDatabaseName("IX_InvoiceReturn_Type_Number");
        builder.HasIndex(r => new { r.ReturnType, r.DateShamsi }).HasDatabaseName("IX_InvoiceReturn_Type_Date");

        builder.Property(r => r.ReturnType).HasMaxLength(20).IsRequired();
        builder.Property(r => r.ReturnNumber).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(30);
        builder.Property(r => r.Reason).HasMaxLength(300);
        builder.Property(r => r.VoucherDocumentNumber).HasMaxLength(50);
        builder.Property(r => r.DateShamsi).HasMaxLength(20);
        builder.Property(r => r.CreatedByUserId).HasMaxLength(450);

        builder.Property(r => r.SubTotal).HasPrecision(18, 2);
        builder.Property(r => r.VatAmount).HasPrecision(18, 2);
        builder.Property(r => r.GrandTotal).HasPrecision(18, 2);

        builder.HasOne(r => r.Invoice)
            .WithMany()
            .HasForeignKey(r => r.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CommercialParty)
            .WithMany(p => p.Returns)
            .HasForeignKey(r => r.CommercialPartyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}