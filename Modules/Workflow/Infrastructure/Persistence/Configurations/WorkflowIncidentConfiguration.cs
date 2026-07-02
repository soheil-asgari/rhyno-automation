using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowIncidentConfiguration : IEntityTypeConfiguration<WorkflowIncident>
{
    public void Configure(EntityTypeBuilder<WorkflowIncident> builder)
    {
        builder.HasIndex(item => new { item.WorkflowInstanceId, item.IsResolved, item.OccurredAt });
        builder.Property(item => item.IncidentType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.ErrorCode).HasMaxLength(120).IsRequired();
        builder.Property(item => item.ErrorMessage).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.ActorUserId).HasMaxLength(450);
        builder.Property(item => item.ResolvedByUserId).HasMaxLength(450);
        builder.Property(item => item.ResolutionNote).HasMaxLength(1000);

        builder
            .HasOne(item => item.WorkflowInstance)
            .WithMany(item => item.Incidents)
            .HasForeignKey(item => item.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(item => item.WorkflowStep)
            .WithMany()
            .HasForeignKey(item => item.WorkflowStepId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasOne(item => item.ActorUser)
            .WithMany()
            .HasForeignKey(item => item.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
