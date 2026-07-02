using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryStockConfiguration : IEntityTypeConfiguration<InventoryStock>
{
    public void Configure(EntityTypeBuilder<InventoryStock> builder)
    {
            builder
                            .HasIndex(stock => new { stock.ProductId, stock.WarehouseId })
                            .IsUnique();

            builder
                            .Property(stock => stock.CurrentQuantity)
                            .HasPrecision(18, 3);

            builder
                            .Property(stock => stock.RowVersion)
                            .IsRowVersion();

            builder
                            .HasOne(stock => stock.Product)
                            .WithMany(product => product.Stocks)
                            .HasForeignKey(stock => stock.ProductId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(stock => stock.Warehouse)
                            .WithMany(warehouse => warehouse.Stocks)
                            .HasForeignKey(stock => stock.WarehouseId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
