using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public static class OrganizationCalendarEventTypes
    {
        public const string Leave = "Leave";
        public const string Occasion = "Occasion";
        public const string Deadline = "Deadline";
        public const string Meeting = "Meeting";
        public const string Payment = "Payment";
        public const string Tax = "Tax";

        public static readonly string[] All =
        [
            Leave,
            Occasion,
            Deadline,
            Meeting,
            Payment,
            Tax
        ];
    }

    public class OrganizationCalendarEvent
    {
        public int Id { get; set; }

        [Required]
        [StringLength(180)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1200)]
        public string? Description { get; set; }

        [Required]
        [StringLength(20)]
        public string EventType { get; set; } = OrganizationCalendarEventTypes.Meeting;

        [Required]
        public DateTime EventDate { get; set; }

        [StringLength(24)]
        public string? EventDateShamsi { get; set; }

        [StringLength(80)]
        public string SourceModule { get; set; } = "Calendar";

        [StringLength(80)]
        public string? SourceEntityType { get; set; }

        public int? SourceEntityId { get; set; }

        public bool IsAllDay { get; set; } = true;

        public bool IsSensitive { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
