using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class VoucherLineConfiguration : IEntityTypeConfiguration<VoucherLine>
{
    public void Configure(EntityTypeBuilder<VoucherLine> builder)
    {
        builder.HasKey(item => item.Id);

        builder.Property(item => item.DebitAmount)
            .HasPrecision(18, 2);

        builder.Property(item => item.CreditAmount)
            .HasPrecision(18, 2);

        builder.Property(item => item.CurrencyId)
            .HasColumnName("CurrencyId");

        builder.Property(item => item.ExchangeRate)
            .HasColumnName("CurrencyRate")
            .HasPrecision(18, 8);

        builder.Property(item => item.ForeignAmount)
            .HasPrecision(18, 4);

        builder.Property(item => item.Narration)
            .HasMaxLength(600);

        builder.HasIndex(item => new { item.VoucherHeaderId, item.DisplayOrder });

        builder.HasIndex(item => item.SubsidiaryAccountId);
        builder.HasIndex(item => item.DetailedAccountId);
        builder.HasIndex(item => item.FloatingDetailAccountId);
        builder.HasIndex(item => new { item.SubsidiaryAccountId, item.FloatingDetailAccountId });

        builder.HasOne(item => item.SubsidiaryAccount)
            .WithMany(item => item.VoucherLines)
            .HasForeignKey(item => item.SubsidiaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.DetailedAccount)
            .WithMany(item => item.VoucherLines)
            .HasForeignKey(item => item.DetailedAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.FloatingDetailAccount)
            .WithMany(item => item.VoucherLines)
            .HasForeignKey(item => item.FloatingDetailAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Currency)
            .WithMany(item => item.VoucherLines)
            .HasForeignKey(item => item.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
