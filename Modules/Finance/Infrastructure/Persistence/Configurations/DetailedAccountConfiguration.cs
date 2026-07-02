using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class DetailedAccountConfiguration : IEntityTypeConfiguration<DetailedAccount>
{
    public void Configure(EntityTypeBuilder<DetailedAccount> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.PartyType).HasMaxLength(50);
        builder.Property(item => item.ExternalReference).HasMaxLength(120);
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => new { item.PartyType, item.ExternalReference });
        builder.HasOne(item => item.SubsidiaryAccount)
            .WithMany(item => item.DetailedAccounts)
            .HasForeignKey(item => item.SubsidiaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
