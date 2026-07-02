using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class ConnectorExecutionLog
{
    public long Id { get; set; }

    [Required]
    [StringLength(80)]
    public string ConnectorName { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string OperationName { get; set; } = string.Empty;

    [StringLength(128)]
    public string? CorrelationId { get; set; }

    public bool Succeeded { get; set; }
    public int AttemptCount { get; set; }
    public long DurationMs { get; set; }

    [StringLength(1200)]
    public string? ErrorMessage { get; set; }

    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
}
