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

        // ── White-labeling / Branding ──────────────────────────────────────────────
        // These fields are stored as per-tenant TenantSetting key-value pairs and
        // resolved by TenantSettingsService. They are rendered as CSS Variables in
        // the Layout _once per request_ using a cached object, avoiding per-page
        // database round-trips.

        /// <summary>Primary accent colour in HSL or hex (e.g. "#0d9488" for teal-600).</summary>
        [MaxLength(32)]
        public string? PrimaryColor { get; set; }

        /// <summary>Secondary/accent colour used for highlights.</summary>
        [MaxLength(32)]
        public string? AccentColor { get; set; }

        /// <summary>URL or data-URI of the organisation logo shown in the sidebar header.</summary>
        [MaxLength(512)]
        public string? LogoUrl { get; set; }

        /// <summary>URL or data-URI of the favicon (.ico / .png / .svg).</summary>
        [MaxLength(512)]
        public string? FaviconUrl { get; set; }

        /// <summary>URL of the background image shown on the login / lock screen.</summary>
        [MaxLength(512)]
        public string? LoginBackgroundUrl { get; set; }

        /// <summary>URL of the background image shown on the main desktop/workspace area.
        /// When empty, the default gradient background from rahkaran-theme.css is used.</summary>
        [MaxLength(512)]
        public string? DesktopBackgroundUrl { get; set; }
    }
}

