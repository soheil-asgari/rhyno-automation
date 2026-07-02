using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
            builder
                            .HasKey(item => item.Key);

            builder
                            .Property(item => item.Key)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.DisplayName)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.Category)
                            .HasMaxLength(64);

            builder
                            .Property(item => item.Description)
                            .HasMaxLength(256);

            builder
                            .HasData(OfficeAutomation.Services.Security.PermissionCatalog.CorePermissions);
    }
}
