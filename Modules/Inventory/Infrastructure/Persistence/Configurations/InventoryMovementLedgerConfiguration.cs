using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryMovementLedgerConfiguration : IEntityTypeConfiguration<InventoryMovementLedger>
{
    public void Configure(EntityTypeBuilder<InventoryMovementLedger> builder)
    {
            builder
                            .Property(item => item.QuantityIn)
                            .HasPrecision(18, 3);

            builder
                            .Property(item => item.QuantityOut)
                            .HasPrecision(18, 3);

            builder
                            .Property(item => item.BalanceAfter)
                            .HasPrecision(18, 3);
    }
}
