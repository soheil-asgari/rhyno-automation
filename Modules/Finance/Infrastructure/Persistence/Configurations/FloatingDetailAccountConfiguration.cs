using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class FloatingDetailAccountConfiguration : IEntityTypeConfiguration<FloatingDetailAccount>
{
    public void Configure(EntityTypeBuilder<FloatingDetailAccount> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => new { item.Type, item.Name });
    }
}
