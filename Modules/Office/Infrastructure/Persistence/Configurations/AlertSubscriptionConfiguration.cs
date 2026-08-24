using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class AlertSubscriptionConfiguration : IEntityTypeConfiguration<AlertSubscription>
{
    public void Configure(EntityTypeBuilder<AlertSubscription> builder)
    {
        builder.ToTable("AlertSubscriptions");

        builder.Property(s => s.TenantId).HasMaxLength(64);
        builder.Property(s => s.UserId).HasMaxLength(450).IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnType("datetimeoffset").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(s => new { s.UserId, s.AlertRuleId }).IsUnique();

        builder.HasOne(s => s.AlertRule)
            .WithMany()
            .HasForeignKey(s => s.AlertRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
