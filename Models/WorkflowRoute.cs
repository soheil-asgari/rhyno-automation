using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class WorkflowRoute
    {
        public int Id { get; set; }

        [Required]
        [StringLength(60)]
        public string DocumentType { get; set; } = string.Empty;

        [Range(1, 20)]
        public int StepNumber { get; set; }

        [Required]
        public string ApproverUserId { get; set; } = string.Empty;

        public User? ApproverUser { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
