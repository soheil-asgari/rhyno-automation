using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowDecisionConfiguration : IEntityTypeConfiguration<WorkflowDecision>
{
    public void Configure(EntityTypeBuilder<WorkflowDecision> builder)
    {
            builder
                            .HasIndex(item => new { item.WorkflowInstanceId, item.DecidedAt });

            builder
                            .Property(item => item.Decision)
                            .HasMaxLength(30);

            builder
                            .Property(item => item.Comment)
                            .HasMaxLength(1000);

            builder
                            .HasOne(item => item.WorkflowInstance)
                            .WithMany(item => item.Decisions)
                            .HasForeignKey(item => item.WorkflowInstanceId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.WorkflowStep)
                            .WithMany()
                            .HasForeignKey(item => item.WorkflowStepId)
                            .OnDelete(DeleteBehavior.NoAction);

            builder
                            .HasOne(item => item.DecidedByUser)
                            .WithMany()
                            .HasForeignKey(item => item.DecidedByUserId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
