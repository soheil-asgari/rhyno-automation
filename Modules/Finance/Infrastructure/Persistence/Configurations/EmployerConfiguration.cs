using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class EmployerConfiguration : IEntityTypeConfiguration<Employer>
{
    public void Configure(EntityTypeBuilder<Employer> builder)
    {
            builder
                            .ToTable("Employers");

            builder
                            .HasIndex(item => item.Name);

            builder
                            .HasIndex(item => item.ContractNumber);

            builder
                            .Property(item => item.Name)
                            .HasMaxLength(150);

            builder
                            .Property(item => item.ContractNumber)
                            .HasMaxLength(50);

            builder
                            .Property(item => item.Phone)
                            .HasMaxLength(20);

            builder
                            .Property(item => item.Address)
                            .HasMaxLength(300);
    }
}
