using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => item.Name);
        builder.HasIndex(item => item.NationalId);
        builder.HasIndex(item => item.EconomicCode);
        builder.HasIndex(item => item.IsActive);

        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.EconomicCode).HasMaxLength(50);
        builder.Property(item => item.NationalId).HasMaxLength(20);
        builder.Property(item => item.Phone).HasMaxLength(20);
        builder.Property(item => item.Address).HasMaxLength(300);
        builder.Property(item => item.Email).HasMaxLength(100);
        builder.Property(item => item.CompanyName).HasMaxLength(200);

        builder.HasMany(item => item.Contracts)
            .WithOne(contract => contract.Customer)
            .HasForeignKey(contract => contract.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}