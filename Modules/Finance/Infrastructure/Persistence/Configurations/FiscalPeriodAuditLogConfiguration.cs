using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class FiscalPeriodAuditLogConfiguration : IEntityTypeConfiguration<FiscalPeriodAuditLog>
{
    public void Configure(EntityTypeBuilder<FiscalPeriodAuditLog> builder)
    {
        builder.ToTable("FiscalPeriodAuditLog");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.PreviousStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.NewStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();

        builder.HasIndex(e => e.FiscalPeriodId);
        builder.HasIndex(e => e.Timestamp);

        builder.HasOne(e => e.FiscalPeriod)
            .WithMany()
            .HasForeignKey(e => e.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}