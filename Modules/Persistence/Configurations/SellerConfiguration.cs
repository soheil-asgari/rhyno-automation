using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Persistence.Configurations;

public sealed class SellerConfiguration : IEntityTypeConfiguration<Seller>
{
    public void Configure(EntityTypeBuilder<Seller> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.EconomicCode).HasMaxLength(50);
        builder.Property(s => s.NationalId).HasMaxLength(20);
        builder.Property(s => s.Phone).HasMaxLength(20);
        builder.Property(s => s.Address).HasMaxLength(400);
        builder.Property(s => s.RegistrationNumber).HasMaxLength(200);

        builder.HasIndex(s => s.EconomicCode).HasDatabaseName("IX_Seller_EconomicCode").IsUnique();
        builder.HasIndex(s => s.NationalId).HasDatabaseName("IX_Seller_NationalId");
    }
}
