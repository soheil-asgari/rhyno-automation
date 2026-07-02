using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
            builder
                            .Property(item => item.Description)
                            .HasMaxLength(256);

            builder
                            .Property(item => item.DataAccessScope)
                            .HasMaxLength(32)
                            .HasDefaultValue(RoleDataAccessScope.Department);
    }
}
