using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class HumanCapitalSalaryHistoryConfiguration : IEntityTypeConfiguration<HumanCapitalSalaryHistory>
{
    public void Configure(EntityTypeBuilder<HumanCapitalSalaryHistory> builder)
    {
            builder
                            .HasOne(history => history.Employee)
                            .WithMany(employee => employee.SalaryHistories)
                            .HasForeignKey(history => history.EmployeeId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasIndex(history => new { history.EmployeeId, history.EffectiveDate });

            builder
                            .Property(history => history.PreviousSalary)
                            .HasPrecision(18, 2);

            builder
                            .Property(history => history.NewSalary)
                            .HasPrecision(18, 2);
    }
}
