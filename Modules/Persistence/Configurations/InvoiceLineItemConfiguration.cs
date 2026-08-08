using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Persistence.Configurations;

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.Property(li => li.Description).HasMaxLength(300).IsRequired();
        builder.Property(li => li.Kind).HasMaxLength(50);
        builder.Property(li => li.Quantity).HasPrecision(18, 3);
        builder.Property(li => li.UnitPrice).HasPrecision(18, 2);
        builder.Property(li => li.LineAmount).HasPrecision(18, 2);
        builder.Property(li => li.RawText).HasMaxLength(500);
    }
}
