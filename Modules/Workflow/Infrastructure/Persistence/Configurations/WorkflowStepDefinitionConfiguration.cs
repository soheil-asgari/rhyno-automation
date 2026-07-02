using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowStepDefinitionConfiguration : IEntityTypeConfiguration<WorkflowStepDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowStepDefinition> builder)
    {
            builder
                            .HasMany(item => item.Rules)
                            .WithOne(item => item.StepDefinition)
                            .HasForeignKey(item => item.StepDefinitionId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .Property(item => item.StepKey)
                            .HasMaxLength(80);

            builder
                            .Property(item => item.AssignmentMode)
                            .HasMaxLength(30)
                            .HasDefaultValue(WorkflowAssignmentMode.User);

            builder
                            .Property(item => item.SlaHours)
                            .HasDefaultValue(24);

            builder
                            .Property(item => item.EscalationHours)
                            .HasDefaultValue(48);

            builder
                            .HasIndex(item => new { item.DefinitionVersionId, item.StepOrder })
                            .IsUnique();

            builder
                            .HasIndex(item => new { item.DefinitionVersionId, item.StepKey })
                            .IsUnique();
    }
}
