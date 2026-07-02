namespace OfficeAutomation.Data;

public interface ITenantSchemaDbContext
{
    string? CurrentDatabaseSchema { get; }
}
