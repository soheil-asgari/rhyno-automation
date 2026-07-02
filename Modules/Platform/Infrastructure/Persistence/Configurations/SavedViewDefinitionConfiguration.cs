using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Platform.Domain;

namespace OfficeAutomation.Modules.Platform.Infrastructure.Persistence.Configurations;

public sealed class SavedViewDefinitionConfiguration : IEntityTypeConfiguration<SavedViewDefinition>
{
    public void Configure(EntityTypeBuilder<SavedViewDefinition> builder)
    {
        builder.ToTable("SavedViewDefinitions");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(item => item.TargetGridId)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(item => item.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(item => item.ColumnLayoutJson)
            .IsRequired();

        builder.Property(item => item.FilterQueryJson)
            .IsRequired();

        builder.HasIndex(item => new { item.TargetGridId, item.UserId, item.Name })
            .IsUnique();
    }
}
