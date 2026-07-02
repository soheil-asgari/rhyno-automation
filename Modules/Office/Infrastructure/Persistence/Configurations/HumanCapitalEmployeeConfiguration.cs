using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class HumanCapitalEmployeeConfiguration : IEntityTypeConfiguration<HumanCapitalEmployee>
{
    public void Configure(EntityTypeBuilder<HumanCapitalEmployee> builder)
    {
            builder
                            .HasOne(employee => employee.Department)
                            .WithMany()
                            .HasForeignKey(employee => employee.DepartmentId)
                            .OnDelete(DeleteBehavior.SetNull);

            builder
                            .HasIndex(employee => employee.PersonnelCode)
                            .IsUnique();

            builder
                            .HasIndex(employee => employee.NationalCode)
                            .IsUnique();

            builder
                            .Property(employee => employee.CurrentSalary)
                            .HasPrecision(18, 2);
    }
}
