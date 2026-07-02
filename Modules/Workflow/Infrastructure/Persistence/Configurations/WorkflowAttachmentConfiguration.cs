using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowAttachmentConfiguration : IEntityTypeConfiguration<WorkflowAttachment>
{
    public void Configure(EntityTypeBuilder<WorkflowAttachment> builder)
    {
            builder
                            .HasOne(item => item.UploadedByUser)
                            .WithMany()
                            .HasForeignKey(item => item.UploadedByUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasIndex(item => new { item.WorkflowInstanceId, item.UploadedAt });
    }
}
