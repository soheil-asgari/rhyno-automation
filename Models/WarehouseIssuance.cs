using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class WarehouseIssuance
    {
        public int Id { get; set; }

        [Required]
        [StringLength(40)]
        public string IssuanceNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string DateShamsi { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string DestinationOrDepartment { get; set; } = string.Empty;

        public int? EmployerId { get; set; }

        [StringLength(600)]
        public string? Notes { get; set; }

        [StringLength(30)]
        public string WorkflowStatus { get; set; } = Models.WorkflowStatus.Draft;

        public int WarehouseId { get; set; }

        public DateTime CreatedAt { get; set; }

        public Warehouse Warehouse { get; set; } = null!;

        public Employer? Employer { get; set; }

        public List<WarehouseIssuanceItem> Items { get; set; } = new();
    }
}
