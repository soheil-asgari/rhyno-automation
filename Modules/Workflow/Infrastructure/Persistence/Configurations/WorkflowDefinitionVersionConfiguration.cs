using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowDefinitionVersionConfiguration : IEntityTypeConfiguration<WorkflowDefinitionVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinitionVersion> builder)
    {
            builder
                            .HasMany(item => item.StepDefinitions)
                            .WithOne(item => item.DefinitionVersion)
                            .HasForeignKey(item => item.DefinitionVersionId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .Property(item => item.DocumentType)
                            .HasMaxLength(60);

            builder
                            .Property(item => item.DeploymentMode)
                            .HasMaxLength(20)
                            .HasDefaultValue(WorkflowDeploymentMode.Stable);

            builder
                            .Property(item => item.TrafficPercentage)
                            .HasDefaultValue(100);

            builder
                            .Property(item => item.DeploymentRing)
                            .HasMaxLength(20);

            builder
                            .Property(item => item.IsActive)
                            .HasDefaultValue(true);

            builder
                            .Property(item => item.EffectiveFrom)
                            .HasColumnType("datetimeoffset");

            builder
                            .Property(item => item.EffectiveTo)
                            .HasColumnType("datetimeoffset");

            builder
                            .HasIndex(item => new { item.DocumentType, item.Version })
                            .IsUnique();

            builder
                            .HasIndex(item => new { item.DocumentType, item.IsActive, item.EffectiveFrom });
    }
}
