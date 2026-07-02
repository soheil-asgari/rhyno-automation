using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
            builder
                            .HasOne(item => item.AssignedToUser)
                            .WithMany()
                            .HasForeignKey(item => item.AssignedToUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.AssignedRole)
                            .WithMany()
                            .HasForeignKey(item => item.AssignedRoleId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.AssignedDepartment)
                            .WithMany()
                            .HasForeignKey(item => item.AssignedDepartmentId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.DelegatedFromUser)
                            .WithMany()
                            .HasForeignKey(item => item.DelegatedFromUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasIndex(item => new { item.AssignedToUserId, item.Status, item.DueAt });

            builder
                            .HasIndex(item => new { item.AssignedRoleId, item.Status, item.DueAt });

            builder
                            .HasIndex(item => new { item.AssignedDepartmentId, item.Status, item.DueAt });

            builder
                            .HasIndex(item => new { item.WorkflowInstanceId, item.Status, item.DueAt });

            builder
                            .HasIndex(item => new { item.ReadAt, item.CreatedAt });

            builder
                            .HasIndex(item => new { item.WorkflowInstanceId, item.StepNumber });

            builder
                            .Property(item => item.Status)
                            .HasMaxLength(30);

            builder
                            .HasOne(item => item.WorkflowInstance)
                            .WithMany(item => item.Steps)
                            .HasForeignKey(item => item.WorkflowInstanceId)
                            .OnDelete(DeleteBehavior.Cascade);

            builder
                            .HasOne(item => item.AssignedToUser)
                            .WithMany()
                            .HasForeignKey(item => item.AssignedToUserId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
