using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Persistence.Configurations;

public sealed class BuyerConfiguration : IEntityTypeConfiguration<Buyer>
{
    public void Configure(EntityTypeBuilder<Buyer> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.EconomicCode).HasMaxLength(50);
        builder.Property(b => b.NationalId).HasMaxLength(20);
        builder.Property(b => b.Phone).HasMaxLength(20);
        builder.Property(b => b.Address).HasMaxLength(400);

        builder.HasIndex(b => b.EconomicCode).HasDatabaseName("IX_Buyer_EconomicCode").IsUnique();
        builder.HasIndex(b => b.NationalId).HasDatabaseName("IX_Buyer_NationalId");
    }
}
