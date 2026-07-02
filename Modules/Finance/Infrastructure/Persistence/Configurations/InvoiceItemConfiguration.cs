using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
            builder
                            .Property(i => i.ItemName)
                            .HasMaxLength(150);

            builder
                            .Property(i => i.Quantity)
                            .HasPrecision(18, 3);

            builder
                            .Property(i => i.UnitPrice)
                            .HasPrecision(18, 2);

            builder
                            .Property(i => i.LineSubTotal)
                            .HasPrecision(18, 2);

            builder
                            .Property(i => i.LineVatAmount)
                            .HasPrecision(18, 2);

            builder
                            .Property(i => i.LineGrandTotal)
                            .HasPrecision(18, 2);

            builder
                            .HasOne(i => i.Invoice)
                            .WithMany(i => i.Items)
                            .HasForeignKey(i => i.InvoiceId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder.Ignore(i => i.Product);
    }
}
