using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public sealed class DatabaseConnectionRequest
    {
        public int? Id { get; set; }

        [StringLength(100)]
        public string? Name { get; set; }

        [Required]
        public string Provider { get; set; } = "SqlServer";

        [Required]
        [StringLength(256)]
        public string Host { get; set; } = string.Empty;

        [Range(0, 65535)]
        public int? Port { get; set; }

        [StringLength(128)]
        public string? DatabaseName { get; set; }

        [StringLength(128)]
        public string? Username { get; set; }

        [StringLength(512)]
        public string? Password { get; set; }

        public bool TrustServerCertificate { get; set; } = true;
    }

    public sealed class SavedDatabaseConnectionDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Provider { get; init; } = string.Empty;
        public string Host { get; init; } = string.Empty;
        public int? Port { get; init; }
        public string? DatabaseName { get; init; }
        public string? Username { get; init; }
        public bool TrustServerCertificate { get; init; }
        public string Endpoint { get; init; } = string.Empty;
    }

    public sealed class AiReportRequest
    {
        [Required]
        [StringLength(1200)]
        public string Prompt { get; set; } = string.Empty;

        [Required]
        public string ChartType { get; set; } = "bar";

        public string? Provider { get; set; }
        public string? DatabaseName { get; set; }
        public DatabaseConnectionRequest? Connection { get; set; }
    }

    public sealed record AiReportResponse
    {
        public string Title { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public IReadOnlyList<string> Labels { get; init; } = [];
        public IReadOnlyList<decimal> Values { get; init; } = [];
        public IReadOnlyList<string> Insights { get; init; } = [];
        public string ChartType { get; init; } = "bar";
        public bool IsFromDatabase { get; init; }
        public string? GeneratedSql { get; init; }
    }
}
