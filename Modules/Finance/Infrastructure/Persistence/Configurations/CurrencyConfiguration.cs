using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(10).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Symbol).HasMaxLength(12).IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
    }
}
