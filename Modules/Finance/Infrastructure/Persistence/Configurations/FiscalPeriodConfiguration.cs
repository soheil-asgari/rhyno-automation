using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
    {
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(item => item.Status)
            .HasMaxLength(20)
            .HasDefaultValue(FiscalPeriodStatus.Open)
            .IsRequired();

        builder.HasIndex(item => new { item.FiscalYearId, item.PeriodNumber })
            .IsUnique();

        builder.HasIndex(item => new { item.FiscalYearId, item.StartDate, item.EndDate });

        builder.HasOne(item => item.FiscalYear)
            .WithMany(item => item.FiscalPeriods)
            .HasForeignKey(item => item.FiscalYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
