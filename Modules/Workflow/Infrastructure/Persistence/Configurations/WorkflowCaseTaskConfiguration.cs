using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowCaseTaskConfiguration : IEntityTypeConfiguration<WorkflowCaseTask>
{
    public void Configure(EntityTypeBuilder<WorkflowCaseTask> builder)
    {
            builder
                            .HasOne(item => item.WorkflowInstance)
                            .WithMany(item => item.CaseTasks)
                            .HasForeignKey(item => item.WorkflowInstanceId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.WorkflowStep)
                            .WithMany(item => item.CaseTasks)
                            .HasForeignKey(item => item.WorkflowStepId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.CreatedByUser)
                            .WithMany()
                            .HasForeignKey(item => item.CreatedByUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.AssignedToUser)
                            .WithMany()
                            .HasForeignKey(item => item.AssignedToUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.SubCaseInstance)
                            .WithMany()
                            .HasForeignKey(item => item.SubCaseInstanceId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasIndex(item => new { item.WorkflowInstanceId, item.Status, item.TaskType });
    }
}
