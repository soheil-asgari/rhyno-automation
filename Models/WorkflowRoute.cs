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
        [StringLength(80)]
        public string? StepName { get; set; }
        [StringLength(30)]
        public string AssignmentMode { get; set; } = WorkflowAssignmentMode.User;

        public string? ApproverUserId { get; set; }

        public User? ApproverUser { get; set; }
        public string? ApproverRoleId { get; set; }
        public ApplicationRole? ApproverRole { get; set; }
        public int? ApproverDepartmentId { get; set; }
        public Department? ApproverDepartment { get; set; }
        public int SlaHours { get; set; } = 24;
        public int EscalationHours { get; set; } = 48;
        public bool AllowDelegation { get; set; } = true;
        public bool AllowReturn { get; set; } = true;

        public bool IsActive { get; set; } = true;
    }
}
