using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowRuleConfiguration : IEntityTypeConfiguration<WorkflowRule>
{
    public void Configure(EntityTypeBuilder<WorkflowRule> builder)
    {
            builder
                            .HasOne(item => item.AssigneeRole)
                            .WithMany()
                            .HasForeignKey(item => item.AssigneeRoleId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.AssigneeUser)
                            .WithMany()
                            .HasForeignKey(item => item.AssigneeUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.AssigneeDepartment)
                            .WithMany()
                            .HasForeignKey(item => item.AssigneeDepartmentId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .Property(item => item.FieldName)
                            .HasMaxLength(120);

            builder
                            .Property(item => item.Operator)
                            .HasMaxLength(40);

            builder
                            .Property(item => item.Value)
                            .HasMaxLength(1000);

            builder
                            .Property(item => item.NextStepKey)
                            .HasMaxLength(80);

            builder
                            .Property(item => item.AssigneeRoleId)
                            .HasMaxLength(450);

            builder
                            .Property(item => item.AssigneeUserId)
                            .HasMaxLength(450);

            builder
                            .HasIndex(item => new { item.StepDefinitionId, item.FieldName, item.Operator });
    }
}
