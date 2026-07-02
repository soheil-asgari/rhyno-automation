using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class CurrencyExchangeRateConfiguration : IEntityTypeConfiguration<CurrencyExchangeRate>
{
    public void Configure(EntityTypeBuilder<CurrencyExchangeRate> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.BuyRate).HasPrecision(18, 8);
        builder.Property(item => item.SellRate).HasPrecision(18, 8);
        builder.HasIndex(item => new { item.CurrencyId, item.RateDate }).IsUnique();
        builder.HasOne(item => item.Currency)
            .WithMany(item => item.ExchangeRates)
            .HasForeignKey(item => item.CurrencyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
