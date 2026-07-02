using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
            builder
                            .HasOne(item => item.CurrentAssigneeUser)
                            .WithMany()
                            .HasForeignKey(item => item.CurrentAssigneeUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.DefinitionVersion)
                            .WithMany(item => item.WorkflowInstances)
                            .HasForeignKey(item => item.DefinitionVersionId)
                            .OnDelete(DeleteBehavior.SetNull);

            builder
                            .HasOne(item => item.ParentWorkflowInstance)
                            .WithMany(item => item.SubCases)
                            .HasForeignKey(item => item.ParentWorkflowInstanceId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.CurrentAssigneeRole)
                            .WithMany()
                            .HasForeignKey(item => item.CurrentAssigneeRoleId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.CurrentAssigneeDepartment)
                            .WithMany()
                            .HasForeignKey(item => item.CurrentAssigneeDepartmentId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasIndex(item => new { item.Status, item.SlaState, item.DueAt });

            builder
                            .HasIndex(item => new { item.CurrentAssigneeUserId, item.Status, item.DueAt });

            builder
                            .HasIndex(item => new { item.DocumentType, item.DocumentId })
                            .IsUnique();

            builder
                            .HasIndex(item => new { item.Status, item.DueAt });

            builder
                            .Property(item => item.DocumentType)
                            .HasMaxLength(60);

            builder
                            .Property(item => item.Status)
                            .HasMaxLength(30);

            builder
                            .Property(item => item.SlaState)
                            .HasMaxLength(30);

            builder
                            .HasIndex(item => item.DefinitionVersionId);

            builder
                            .HasIndex(item => new { item.DefinitionVersionId, item.Status });

            builder
                            .HasOne(item => item.StartedByUser)
                            .WithMany()
                            .HasForeignKey(item => item.StartedByUserId)
                            .OnDelete(DeleteBehavior.SetNull);
    }
}
