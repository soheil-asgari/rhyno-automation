using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Commercial.Domain;

namespace OfficeAutomation.Modules.Commercial.Infrastructure.Persistence.Configurations;

public sealed class CommercialQuotationConfiguration : IEntityTypeConfiguration<CommercialQuotation>
{
    public void Configure(EntityTypeBuilder<CommercialQuotation> builder)
    {
        builder.HasIndex(q => new { q.QuotationType, q.QuotationNumber }).IsUnique().HasDatabaseName("IX_CommercialQuotation_Type_Number");
        builder.HasIndex(q => new { q.QuotationType, q.DateShamsi }).HasDatabaseName("IX_CommercialQuotation_Type_Date");

        builder.Property(q => q.QuotationType).HasMaxLength(20).IsRequired();
        builder.Property(q => q.QuotationNumber).HasMaxLength(50).IsRequired();
        builder.Property(q => q.PartyName).HasMaxLength(150);
        builder.Property(q => q.NationalCodeOrEconomicId).HasMaxLength(30);
        builder.Property(q => q.DateShamsi).HasMaxLength(20);
        builder.Property(q => q.ValidUntilShamsi).HasMaxLength(20);
        builder.Property(q => q.Status).HasMaxLength(30);
        builder.Property(q => q.Notes).HasMaxLength(400);
        builder.Property(q => q.CreatedByUserId).HasMaxLength(450);

        builder.Property(q => q.SubTotal).HasPrecision(18, 2);
        builder.Property(q => q.VatAmount).HasPrecision(18, 2);
        builder.Property(q => q.GrandTotal).HasPrecision(18, 2);
        builder.Property(q => q.DiscountAmount).HasPrecision(18, 2);

        builder.HasOne(q => q.CommercialParty)
            .WithMany(p => p.Quotations)
            .HasForeignKey(q => q.CommercialPartyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}