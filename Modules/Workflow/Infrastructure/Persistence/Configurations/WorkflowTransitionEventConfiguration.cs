using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowTransitionEventConfiguration : IEntityTypeConfiguration<WorkflowTransitionEvent>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionEvent> builder)
    {
            builder
                            .HasOne(item => item.WorkflowInstance)
                            .WithMany(item => item.TransitionEvents)
                            .HasForeignKey(item => item.WorkflowInstanceId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.WorkflowStep)
                            .WithMany(item => item.TransitionEvents)
                            .HasForeignKey(item => item.WorkflowStepId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.ActorUser)
                            .WithMany()
                            .HasForeignKey(item => item.ActorUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasIndex(item => new { item.WorkflowInstanceId, item.SequenceNumber })
                            .IsUnique();

            builder
                            .HasIndex(item => new { item.StationKey, item.OccurredAt });
    }
}
