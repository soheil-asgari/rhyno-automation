using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
            builder
                            .HasOne(d => d.Manager)
                            .WithMany()
                            .HasForeignKey(d => d.ManagerId)
                            .IsRequired(false)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(d => d.ManagerEmployee)
                            .WithMany()
                            .HasForeignKey(d => d.ManagerEmployeeId)
                            .OnDelete(DeleteBehavior.SetNull);

            builder.HasData(
                            new Department { Id = 1, Name = "Financial" },
                            new Department { Id = 2, Name = "Administrative" },
                            new Department { Id = 3, Name = "Technical" },
                            new Department { Id = 4, Name = "HR" },
                            new Department { Id = 5, Name = "Management" }
                        );
    }
}
