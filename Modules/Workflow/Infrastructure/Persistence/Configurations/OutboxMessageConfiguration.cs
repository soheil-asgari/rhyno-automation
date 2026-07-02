using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
            builder
                            .Property(item => item.MessageId)
                            .HasMaxLength(80)
                            .IsRequired();

            builder
                            .Property(item => item.TenantId)
                            .HasMaxLength(64);

            builder
                            .Property(item => item.EventType)
                            .HasMaxLength(120);

            builder
                            .Property(item => item.AggregateType)
                            .HasMaxLength(120);

            builder
                            .Property(item => item.AggregateId)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.ExchangeName)
                            .HasMaxLength(120);

            builder
                            .Property(item => item.RoutingKey)
                            .HasMaxLength(160);

            builder
                            .Property(item => item.Status)
                            .HasMaxLength(32)
                            .HasDefaultValue(OutboxMessageStatus.Pending);

            builder
                            .Property(item => item.CorrelationId)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.LastError)
                            .HasMaxLength(1200);

            builder
                            .Property(item => item.OccurredAt)
                            .HasColumnType("datetimeoffset")
                            .HasDefaultValueSql("SYSUTCDATETIME()");

            builder
                            .Property(item => item.LockedUntil)
                            .HasColumnType("datetimeoffset");

            builder
                            .Property(item => item.ProcessedAt)
                            .HasColumnType("datetimeoffset");

            builder
                            .Property(item => item.LastAttemptAt)
                            .HasColumnType("datetimeoffset");

            builder
                            .HasIndex(item => item.MessageId)
                            .IsUnique();

            builder
                            .HasIndex(item => new { item.Status, item.LockedUntil, item.OccurredAt });

            builder
                            .HasIndex(item => new { item.AggregateType, item.AggregateId, item.OccurredAt });

            builder
                            .HasIndex(item => new { item.TenantId, item.Status, item.LockedUntil, item.OccurredAt });
    }
}
