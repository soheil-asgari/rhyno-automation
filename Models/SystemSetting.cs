using System;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class SystemSetting
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان سامانه الزامی است.")]
        [MaxLength(120)]
        public string ApplicationTitle { get; set; } = "Rhyno Dashboard";

        [Required(ErrorMessage = "زبان سامانه الزامی است.")]
        [MaxLength(16)]
        public string SystemLanguage { get; set; } = "fa-IR";

        [Required(ErrorMessage = "منطقه زمانی سامانه الزامی است.")]
        [MaxLength(120)]
        public string TimeZoneId { get; set; } = "Asia/Tehran";

        [MaxLength(64)]
        public string? ActiveEnvironment { get; set; }

        public bool MaintenanceMode { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
