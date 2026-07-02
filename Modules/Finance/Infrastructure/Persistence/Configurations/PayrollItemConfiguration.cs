using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class PayrollItemConfiguration : IEntityTypeConfiguration<PayrollItem>
{
    public void Configure(EntityTypeBuilder<PayrollItem> builder)
    {
            builder
                            .Property(item => item.EmployeeName)
                            .HasMaxLength(120);

            builder
                            .Property(item => item.BaseSalary)
                            .HasPrecision(18, 2);

            builder
                            .Property(item => item.Allowance)
                            .HasPrecision(18, 2);

            builder
                            .Property(item => item.Overtime)
                            .HasPrecision(18, 2);

            builder
                            .Property(item => item.InsuranceDeduction)
                            .HasPrecision(18, 2);

            builder
                            .Property(item => item.Tax)
                            .HasPrecision(18, 2);

            builder
                            .Property(item => item.NetPayable)
                            .HasPrecision(18, 2);

            builder
                            .HasOne(item => item.PayrollList)
                            .WithMany(list => list.Items)
                            .HasForeignKey(item => item.PayrollListId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.HumanCapitalEmployee)
                            .WithMany()
                            .HasForeignKey(item => item.HumanCapitalEmployeeId)
                            .OnDelete(DeleteBehavior.SetNull);
    }
}
