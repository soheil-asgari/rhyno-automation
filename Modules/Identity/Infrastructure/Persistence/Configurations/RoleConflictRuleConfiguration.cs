using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RoleConflictRuleConfiguration : IEntityTypeConfiguration<RoleConflictRule>
{
    public void Configure(EntityTypeBuilder<RoleConflictRule> builder)
    {
            builder
                            .Property(item => item.RoleA)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.RoleB)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.Reason)
                            .HasMaxLength(256);

            builder
                            .HasIndex(item => new { item.RoleA, item.RoleB })
                            .IsUnique();
    }
}
