using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class ConnectorDeadLetterMessageConfiguration : IEntityTypeConfiguration<ConnectorDeadLetterMessage>
{
    public void Configure(EntityTypeBuilder<ConnectorDeadLetterMessage> builder)
    {
            builder
                            .HasIndex(item => new { item.Status, item.FailedAt });
    }
}
