using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowRouteConfiguration : IEntityTypeConfiguration<WorkflowRoute>
{
    public void Configure(EntityTypeBuilder<WorkflowRoute> builder)
    {
            builder
                            .HasOne(item => item.ApproverUser)
                            .WithMany()
                            .HasForeignKey(item => item.ApproverUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.ApproverRole)
                            .WithMany()
                            .HasForeignKey(item => item.ApproverRoleId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.ApproverDepartment)
                            .WithMany()
                            .HasForeignKey(item => item.ApproverDepartmentId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .Property(item => item.DocumentType)
                            .HasMaxLength(60);

            builder
                            .HasIndex(item => new { item.DocumentType, item.StepNumber });

            builder
                            .HasOne(item => item.ApproverUser)
                            .WithMany()
                            .HasForeignKey(item => item.ApproverUserId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
