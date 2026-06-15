using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public sealed class OrganizationCalendarEventVM
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(180)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1200)]
        public string? Description { get; set; }

        [Required]
        [StringLength(20)]
        public string EventType { get; set; } = OrganizationCalendarEventTypes.Meeting;

        [Required]
        [StringLength(20)]
        public string EventDateShamsi { get; set; } = string.Empty;

        [StringLength(80)]
        public string SourceModule { get; set; } = "Calendar";

        public bool IsAllDay { get; set; } = true;
        public bool IsSensitive { get; set; }
    }

    public sealed class CalendarEventItemVM
    {
        public int? Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string EventType { get; init; } = string.Empty;
        public string EventTypeTitle { get; init; } = string.Empty;
        public string DateShamsi { get; init; } = string.Empty;
        public DateTime DateGregorian { get; init; }
        public string SourceModule { get; init; } = string.Empty;
        public bool IsSystemGenerated { get; init; }
        public bool IsSensitive { get; init; }
    }

    public sealed class OrganizationCalendarIndexVM
    {
        public string MonthTitle { get; init; } = string.Empty;
        public int CurrentYear { get; init; }
        public int CurrentMonth { get; init; }
        public IReadOnlyList<CalendarEventItemVM> Events { get; init; } = [];
        public OrganizationCalendarEventVM NewEvent { get; init; } = new();
    }
}
