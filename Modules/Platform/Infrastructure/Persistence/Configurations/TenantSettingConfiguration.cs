using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
            builder
                            .Property(item => item.TenantId)
                            .HasMaxLength(64);

            builder
                            .Property(item => item.Key)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.Value)
                            .HasMaxLength(2000);

            builder
                            .HasIndex(item => new { item.TenantId, item.Key })
                            .IsUnique();
    }
}
