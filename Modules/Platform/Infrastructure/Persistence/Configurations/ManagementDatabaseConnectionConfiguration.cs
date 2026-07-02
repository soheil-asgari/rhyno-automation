using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Platform.Infrastructure.Persistence.Configurations;

public sealed class ManagementDatabaseConnectionConfiguration : IEntityTypeConfiguration<ManagementDatabaseConnection>
{
    public void Configure(EntityTypeBuilder<ManagementDatabaseConnection> builder)
    {
            builder
                            .Property(item => item.Name)
                            .HasMaxLength(100);

            builder
                            .Property(item => item.Provider)
                            .HasMaxLength(40);

            builder
                            .Property(item => item.Host)
                            .HasMaxLength(256);

            builder
                            .Property(item => item.DatabaseName)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.Username)
                            .HasMaxLength(128);

            builder
                            .Property(item => item.CreatedByUserId)
                            .HasMaxLength(100);

            builder
                            .HasIndex(item => item.Name);
    }
}
