using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class PayrollListConfiguration : IEntityTypeConfiguration<PayrollList>
{
    public void Configure(EntityTypeBuilder<PayrollList> builder)
    {
            builder
                            .HasIndex(list => new { list.Year, list.Month })
                            .IsUnique();

            builder
                            .Property(list => list.Status)
                            .HasMaxLength(50);

            builder
                            .Property(list => list.RowVersion)
                            .IsRowVersion();
    }
}
