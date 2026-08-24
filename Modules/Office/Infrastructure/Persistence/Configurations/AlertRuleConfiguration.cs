using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("AlertRules");

        builder.Property(r => r.TenantId).HasMaxLength(64);
        builder.Property(r => r.SourceModule).HasMaxLength(60).IsRequired();
        builder.Property(r => r.TriggerType).HasMaxLength(80).IsRequired();
        builder.Property(r => r.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(1000);
        builder.Property(r => r.Severity).HasMaxLength(20).IsRequired().HasDefaultValue(NotificationSeverity.Info);
        builder.Property(r => r.FilterConditions).HasMaxLength(2000);
        builder.Property(r => r.CreatedByUserId).HasMaxLength(450);
        builder.Property(r => r.CreatedAt).HasColumnType("datetimeoffset").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(r => r.TriggerType).IsUnique().HasFilter("[TenantId] IS NULL");
        builder.HasIndex(r => r.SourceModule);
        builder.HasIndex(r => r.IsActive);

        builder.HasOne(r => r.CreatedByUser)
            .WithMany()
            .HasForeignKey(r => r.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
