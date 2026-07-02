using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowSlaJobConfiguration : IEntityTypeConfiguration<WorkflowSlaJob>
{
    public void Configure(EntityTypeBuilder<WorkflowSlaJob> builder)
    {
            builder
                            .HasOne(item => item.WorkflowInstance)
                            .WithMany(item => item.SlaJobs)
                            .HasForeignKey(item => item.WorkflowInstanceId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.WorkflowStep)
                            .WithMany(item => item.SlaJobs)
                            .HasForeignKey(item => item.WorkflowStepId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasIndex(item => new { item.Status, item.ScheduledFor });

            builder
                            .HasIndex(item => new { item.WorkflowStepId, item.Status });
    }
}
