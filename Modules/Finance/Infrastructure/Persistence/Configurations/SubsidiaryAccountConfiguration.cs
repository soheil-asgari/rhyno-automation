using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class SubsidiaryAccountConfiguration : IEntityTypeConfiguration<SubsidiaryAccount>
{
    public void Configure(EntityTypeBuilder<SubsidiaryAccount> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(30).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.SystemKey).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Nature)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AccountNature.NoControl)
            .IsRequired();
        builder.Property(item => item.IsTemporary)
            .HasDefaultValue(false)
            .IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => item.SystemKey).IsUnique();
        builder.HasOne(item => item.GeneralAccount)
            .WithMany(item => item.SubsidiaryAccounts)
            .HasForeignKey(item => item.GeneralAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
