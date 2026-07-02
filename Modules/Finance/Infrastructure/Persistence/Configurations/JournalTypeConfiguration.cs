using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class JournalTypeConfiguration : IEntityTypeConfiguration<JournalType>
{
    public void Configure(EntityTypeBuilder<JournalType> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(30).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
    }
}
