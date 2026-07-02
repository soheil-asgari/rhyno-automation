using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantBackgroundJobStateConfiguration : IEntityTypeConfiguration<TenantBackgroundJobState>
{
    public void Configure(EntityTypeBuilder<TenantBackgroundJobState> builder)
    {
            builder
                            .Property(item => item.TenantId)
                            .HasMaxLength(64);

            builder
                            .Property(item => item.JobName)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.JobNamespace)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.LockedBy)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.LastError)
                            .HasMaxLength(1200);

            builder
                            .HasIndex(item => new { item.TenantId, item.JobNamespace, item.JobName })
                            .IsUnique();
    }
}
