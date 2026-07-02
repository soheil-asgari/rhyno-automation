using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
            builder
                            .HasIndex(product => product.Code)
                            .IsUnique();

            builder
                            .Property(product => product.Code)
                            .HasMaxLength(40);

            builder
                            .Property(product => product.Name)
                            .HasMaxLength(150);

            builder
                            .Property(product => product.Unit)
                            .HasMaxLength(30);

            builder
                            .Property(product => product.Description)
                            .HasMaxLength(600);

            builder
                            .Property(product => product.ReorderPoint)
                            .HasPrecision(18, 2);

            builder
                            .Property(product => product.MaximumStock)
                            .HasPrecision(18, 2);

            builder
                            .Property(product => product.LastPurchasePrice)
                            .HasPrecision(18, 2);

            builder
                            .Property(product => product.MinimumStock)
                            .HasDefaultValue(0);
    }
}
