using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
            builder
                            .Property(item => item.TenantId)
                            .HasMaxLength(64);

            builder
                            .Property(item => item.RecipientUserId)
                            .HasMaxLength(450);

            builder
                            .Property(item => item.Title)
                            .HasMaxLength(180);

            builder
                            .Property(item => item.Message)
                            .HasMaxLength(600);

            builder
                            .Property(item => item.Severity)
                            .HasMaxLength(20)
                            .HasDefaultValue(NotificationSeverity.Info);

            builder
                            .Property(item => item.LinkUrl)
                            .HasMaxLength(400);

            builder
                            .Property(item => item.SourceModule)
                            .HasMaxLength(80);

            builder
                            .Property(item => item.SourceEntityType)
                            .HasMaxLength(120);

            builder
                            .Property(item => item.CreatedAt)
                            .HasColumnType("datetimeoffset")
                            .HasDefaultValueSql("SYSUTCDATETIME()");

            builder
                            .Property(item => item.ReadAt)
                            .HasColumnType("datetimeoffset");

            builder
                            .Property(item => item.ExpiresAt)
                            .HasColumnType("datetimeoffset");

            builder
                            .HasIndex(item => new { item.RecipientUserId, item.IsRead, item.CreatedAt });

            builder
                            .HasIndex(item => new { item.TenantId, item.RecipientUserId, item.CreatedAt });

            builder
                            .HasOne(item => item.RecipientUser)
                            .WithMany()
                            .HasForeignKey(item => item.RecipientUserId)
                            .OnDelete(DeleteBehavior.Cascade);
    }
}
