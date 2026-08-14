using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Commercial.Domain;

namespace OfficeAutomation.Modules.Commercial.Infrastructure.Persistence.Configurations;

public sealed class CommercialPartyConfiguration : IEntityTypeConfiguration<CommercialParty>
{
    public void Configure(EntityTypeBuilder<CommercialParty> builder)
    {
        builder.HasIndex(p => p.Name).HasDatabaseName("IX_CommercialParty_Name");

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.NationalCode).HasMaxLength(12);
        builder.Property(p => p.EconomicCode).HasMaxLength(50);
        builder.Property(p => p.Phone).HasMaxLength(30);
        builder.Property(p => p.Email).HasMaxLength(150);
        builder.Property(p => p.Address).HasMaxLength(400);
        builder.Property(p => p.Notes).HasMaxLength(400);
        builder.Property(p => p.CreatedByUserId).HasMaxLength(450);
    }
}