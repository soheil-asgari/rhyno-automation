using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models
{
    public class SecurityRoleEditVM
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class SecurityPermissionToggleVM
    {
        [Required]
        public string RoleId { get; set; } = string.Empty;

        [Required]
        [StringLength(128)]
        public string PermissionKey { get; set; } = string.Empty;

        public bool IsAllowed { get; set; }
    }

    public class SecurityMatrixVM
    {
        public List<SecurityRoleVM> Roles { get; set; } = new();

        public List<PermissionFeatureVM> Features { get; set; } = new();
    }

    public class SecurityRoleVM
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    public class PermissionFeatureVM
    {
        public string Key { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
    }

    public class InventoryTransferRequestCreateVM
    {
        [Range(1, int.MaxValue)]
        [Display(Name = "انبار مبدا")]
        public int SourceWarehouseId { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "انبار مقصد")]
        public int DestinationWarehouseId { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "کالا")]
        public int ProductId { get; set; }

        [Range(0.001, 99999999999)]
        [Display(Name = "مقدار")]
        public decimal Quantity { get; set; }

        [StringLength(600)]
        [Display(Name = "توضیح")]
        public string? Description { get; set; }

        public List<SelectListItem> WarehouseOptions { get; set; } = new();

        public List<SelectListItem> ProductOptions { get; set; } = new();
    }

    public class ReactFlowNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "ApprovalNode"; // StartEndNode, ApprovalNode, ConditionNode, WebhookNode
        public string Label { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public string? AssignmentMode { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedRoleId { get; set; }
        public int? AssignedDepartmentId { get; set; }
        public int SlaHours { get; set; } = 24;
        public int EscalationHours { get; set; } = 48;
    }

    public class ReactFlowEdgeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string? Condition { get; set; }
    }

    public class ReactFlowGraphDto
    {
        public string DocumentType { get; set; } = string.Empty;
        public List<ReactFlowNodeDto> Nodes { get; set; } = new();
        public List<ReactFlowEdgeDto> Edges { get; set; } = new();
    }
}
