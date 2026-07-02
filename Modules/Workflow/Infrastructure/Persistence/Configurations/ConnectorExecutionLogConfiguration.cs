using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class ConnectorExecutionLogConfiguration : IEntityTypeConfiguration<ConnectorExecutionLog>
{
    public void Configure(EntityTypeBuilder<ConnectorExecutionLog> builder)
    {
            builder
                            .HasIndex(item => new { item.ConnectorName, item.ExecutedAt });
    }
}
