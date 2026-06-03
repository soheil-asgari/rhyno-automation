using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [StringLength(100)]
        public string? UserId { get; set; }

        [Required]
        [StringLength(20)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(80)]
        public string EntityName { get; set; } = string.Empty;

        [Required]
        [StringLength(80)]
        public string EntityId { get; set; } = string.Empty;

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
