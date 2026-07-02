using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowDelegationConfiguration : IEntityTypeConfiguration<WorkflowDelegation>
{
    public void Configure(EntityTypeBuilder<WorkflowDelegation> builder)
    {
            builder
                            .HasIndex(item => new { item.FromUserId, item.StartsAt, item.EndsAt });

            builder
                            .Property(item => item.DocumentType)
                            .HasMaxLength(60);

            builder
                            .HasOne(item => item.FromUser)
                            .WithMany()
                            .HasForeignKey(item => item.FromUserId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(item => item.ToUser)
                            .WithMany()
                            .HasForeignKey(item => item.ToUserId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
