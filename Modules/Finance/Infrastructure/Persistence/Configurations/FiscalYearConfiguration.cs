using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.HasKey(item => item.Id);

        builder.Property(item => item.YearName)
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(item => item.YearName)
            .IsUnique();

        builder.HasIndex(item => new { item.StartDate, item.EndDate });
    }
}
