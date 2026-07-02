using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class InsuranceEmployeeConfiguration : IEntityTypeConfiguration<InsuranceEmployee>
{
    public void Configure(EntityTypeBuilder<InsuranceEmployee> builder)
    {
            builder
                            .Property(emp => emp.Salary)
                            .HasPrecision(18, 2);

            builder
                            .HasOne(emp => emp.HumanCapitalEmployee)
                            .WithMany()
                            .HasForeignKey(emp => emp.HumanCapitalEmployeeId)
                            .OnDelete(DeleteBehavior.SetNull);
    }
}
