using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Commercial.Domain;

namespace OfficeAutomation.Modules.Commercial.Infrastructure.Persistence.Configurations;

public sealed class CommercialQuotationLineConfiguration : IEntityTypeConfiguration<CommercialQuotationLine>
{
    public void Configure(EntityTypeBuilder<CommercialQuotationLine> builder)
    {
        builder.Property(x => x.ItemName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DiscountRate).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineSubTotal).HasPrecision(18, 2);
        builder.Property(x => x.LineVatAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineGrandTotal).HasPrecision(18, 2);

        builder.HasOne(x => x.CommercialQuotation)
            .WithMany(q => q.Lines)
            .HasForeignKey(x => x.CommercialQuotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}