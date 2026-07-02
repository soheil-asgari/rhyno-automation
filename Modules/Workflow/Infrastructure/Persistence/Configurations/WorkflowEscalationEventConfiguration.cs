using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowEscalationEventConfiguration : IEntityTypeConfiguration<WorkflowEscalationEvent>
{
    public void Configure(EntityTypeBuilder<WorkflowEscalationEvent> builder)
    {
            builder
                            .HasOne(item => item.EscalatedToUser)
                            .WithMany()
                            .HasForeignKey(item => item.EscalatedToUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.EscalatedToRole)
                            .WithMany()
                            .HasForeignKey(item => item.EscalatedToRoleId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
