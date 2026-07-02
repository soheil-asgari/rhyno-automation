using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowActionLogConfiguration : IEntityTypeConfiguration<WorkflowActionLog>
{
    public void Configure(EntityTypeBuilder<WorkflowActionLog> builder)
    {
            builder
                            .HasOne(item => item.ActorUser)
                            .WithMany()
                            .HasForeignKey(item => item.ActorUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasIndex(item => new { item.WorkflowInstanceId, item.OccurredAt });
    }
}
