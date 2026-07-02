using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantDefinitionConfiguration : IEntityTypeConfiguration<TenantDefinition>
{
    public void Configure(EntityTypeBuilder<TenantDefinition> builder)
    {
        builder.HasKey(item => item.TenantId);

        builder.Property(item => item.TenantId).HasMaxLength(64);
        builder.Property(item => item.Name).HasMaxLength(128).IsRequired();
        builder.Property(item => item.IsolationMode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ConnectionString).IsRequired();
        builder.Property(item => item.LifecycleState).HasMaxLength(32).IsRequired();
        builder.Property(item => item.SchemaVersion).HasMaxLength(64);
        builder.Property(item => item.Plan).HasMaxLength(64);
        builder.Property(item => item.DatabaseSchema).HasMaxLength(128);
        builder.Property(item => item.QueueNamespace).HasMaxLength(128);
        builder.Property(item => item.CachePrefix).HasMaxLength(128);
        builder.Property(item => item.StorageRoot).HasMaxLength(256);
        builder.Property(item => item.LogPrefix).HasMaxLength(128);
        builder.Property(item => item.LogRoot).HasMaxLength(260);
        builder.Property(item => item.SettingsNamespace).HasMaxLength(128);
        builder.Property(item => item.JobNamespace).HasMaxLength(128);

        builder.HasIndex(item => item.LifecycleState);
    }
}
