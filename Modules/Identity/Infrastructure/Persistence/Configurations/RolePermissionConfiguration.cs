using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
            builder
                            .Property(item => item.PermissionKey)
                            .HasMaxLength(80);

            builder
                            .Property(item => item.PermissionKey)
                            .HasMaxLength(128);

            builder
                            .HasIndex(item => new { item.RoleId, item.PermissionKey })
                            .IsUnique();

            builder
                            .HasOne(item => item.Role)
                            .WithMany()
                            .HasForeignKey(item => item.RoleId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasIndex(item => new { item.RoleId, item.PermissionKey })
                            .IsUnique();

            builder
                            .HasOne(item => item.Role)
                            .WithMany()
                            .HasForeignKey(item => item.RoleId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.Permission)
                            .WithMany()
                            .HasForeignKey(item => item.PermissionKey)
                            .HasPrincipalKey(item => item.Key)
                            .OnDelete(DeleteBehavior.Cascade);
    }
}
