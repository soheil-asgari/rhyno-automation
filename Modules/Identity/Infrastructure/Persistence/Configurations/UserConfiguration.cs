using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
            builder
                            .HasOne(u => u.Department)
                            .WithMany(d => d.Users)
                            .HasForeignKey(u => u.DepartmentId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(u => u.Employee)
                            .WithMany()
                            .HasForeignKey(u => u.EmployeeId)
                            .OnDelete(DeleteBehavior.SetNull);

            builder
                            .HasOne(u => u.ParentManagerUser)
                            .WithMany()
                            .HasForeignKey(u => u.ParentManagerUserId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
