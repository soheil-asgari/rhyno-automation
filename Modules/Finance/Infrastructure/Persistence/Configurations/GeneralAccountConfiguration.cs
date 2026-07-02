using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class GeneralAccountConfiguration : IEntityTypeConfiguration<GeneralAccount>
{
    public void Configure(EntityTypeBuilder<GeneralAccount> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.Nature)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AccountNature.NoControl)
            .IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasOne(item => item.AccountGroup)
            .WithMany(item => item.GeneralAccounts)
            .HasForeignKey(item => item.AccountGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
