using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Commercial.Domain;

namespace OfficeAutomation.Modules.Commercial.Infrastructure.Persistence.Configurations;

public sealed class InvoiceReturnItemConfiguration : IEntityTypeConfiguration<InvoiceReturnItem>
{
    public void Configure(EntityTypeBuilder<InvoiceReturnItem> builder)
    {
        builder.Property(x => x.ItemName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineSubTotal).HasPrecision(18, 2);
        builder.Property(x => x.LineVatAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineGrandTotal).HasPrecision(18, 2);

        builder.HasOne(x => x.InvoiceReturn)
            .WithMany(r => r.Items)
            .HasForeignKey(x => x.InvoiceReturnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}