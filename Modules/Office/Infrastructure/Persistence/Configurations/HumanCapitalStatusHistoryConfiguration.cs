using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class HumanCapitalStatusHistoryConfiguration : IEntityTypeConfiguration<HumanCapitalStatusHistory>
{
    public void Configure(EntityTypeBuilder<HumanCapitalStatusHistory> builder)
    {
            builder
                            .HasOne(history => history.Employee)
                            .WithMany(employee => employee.StatusHistories)
                            .HasForeignKey(history => history.EmployeeId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasIndex(history => new { history.EmployeeId, history.EffectiveDate });
    }
}
