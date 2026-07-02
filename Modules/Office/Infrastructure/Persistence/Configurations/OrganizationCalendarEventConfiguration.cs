using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class OrganizationCalendarEventConfiguration : IEntityTypeConfiguration<OrganizationCalendarEvent>
{
    public void Configure(EntityTypeBuilder<OrganizationCalendarEvent> builder)
    {
            builder
                            .Property(item => item.Title)
                            .HasMaxLength(180);

            builder
                            .Property(item => item.Description)
                            .HasMaxLength(1200);

            builder
                            .Property(item => item.EventType)
                            .HasMaxLength(20);

            builder
                            .Property(item => item.EventDateShamsi)
                            .HasMaxLength(24);

            builder
                            .Property(item => item.SourceModule)
                            .HasMaxLength(80);

            builder
                            .Property(item => item.SourceEntityType)
                            .HasMaxLength(80);

            builder
                .Property(item => item.CreatedByUserId)
                .HasMaxLength(450);

            builder
                            .HasIndex(item => new { item.EventDate, item.EventType });
    }
}
