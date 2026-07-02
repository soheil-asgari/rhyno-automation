using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
            builder
                            .ToTable("Vendors");

            builder
                            .HasIndex(item => item.Name);

            builder
                            .HasIndex(item => item.EconomicCode);

            builder
                            .Property(item => item.Name)
                            .HasMaxLength(150);

            builder
                            .Property(item => item.EconomicCode)
                            .HasMaxLength(50);

            builder
                            .Property(item => item.NationalId)
                            .HasMaxLength(20);

            builder
                            .Property(item => item.Phone)
                            .HasMaxLength(20);

            builder
                            .Property(item => item.Address)
                            .HasMaxLength(300);
    }
}
