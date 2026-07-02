using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class WaybillConfiguration : IEntityTypeConfiguration<Waybill>
{
    public void Configure(EntityTypeBuilder<Waybill> builder)
    {
            builder
                            .HasIndex(waybill => waybill.WaybillNumber)
                            .IsUnique();

            builder
                            .Property(waybill => waybill.Weight)
                            .HasPrecision(18, 3);

            builder
                            .Property(waybill => waybill.TotalFreightCharges)
                            .HasPrecision(18, 2);

            builder
                            .Property(waybill => waybill.DriverCommission)
                            .HasPrecision(18, 2);

            builder
                            .Property(waybill => waybill.NetPayToDriver)
                            .HasPrecision(18, 2);
    }
}
