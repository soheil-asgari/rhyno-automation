using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Platform.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
            builder
                            .Property(item => item.TenantId)
                            .HasMaxLength(64);

            builder
                            .Property(item => item.UserId)
                            .HasMaxLength(100);

            builder
                            .Property(item => item.Action)
                            .HasMaxLength(20);

            builder
                            .Property(item => item.TableName)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.EntityId)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.CorrelationId)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.UserContext);

            builder
                            .Property(item => item.ChangeSet);

            builder
                            .Property(item => item.IntegrityHash)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.DateTime)
                            .HasColumnType("datetimeoffset")
                            .HasDefaultValueSql("SYSUTCDATETIME()");

            builder
                            .Property(item => item.UserIP)
                            .HasMaxLength(64);

            builder
                            .Property(item => item.UserAgent)
                            .HasMaxLength(1024);

            builder
                            .Property(item => item.IsSensitive)
                            .HasDefaultValue(false);

            builder
                            .HasIndex(item => new { item.TableName, item.DateTime });

            builder
                            .HasIndex(item => new { item.TableName, item.EntityId, item.DateTime });

            builder
                            .HasIndex(item => item.CorrelationId);

            builder
                            .HasIndex(item => new { item.TenantId, item.DateTime });
    }
}
