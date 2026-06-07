using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models
{
    public class WarehouseDashboardVM
    {
        public int ProductCount { get; set; }

        public int WarehouseCount { get; set; }

        public int ReceiptCount { get; set; }

        public int IssuanceCount { get; set; }

        public int CountingDraftCount { get; set; }

        public int LowStockCount { get; set; }
    }

    public class ProductUpsertVM
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(40)]
        [Display(Name = "کد کالا")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [Display(Name = "نام کالا")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        [Display(Name = "واحد")]
        public string Unit { get; set; } = string.Empty;

        [StringLength(600)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [Range(0, 999999)]
        [Display(Name = "حداقل موجودی")]
        public int MinimumStock { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;
    }

    public class WarehouseUpsertVM
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "کد انبار")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        [Display(Name = "نام انبار")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "موقعیت")]
        public string? Location { get; set; }

        [Display(Name = "مدیر انبار")]
        public string? ManagerUserId { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "بسته")]
        public bool IsClosed { get; set; }

        public List<SelectListItem> ManagerOptions { get; set; } = new();
    }

    public class WarehouseReceiptUpsertVM
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(40)]
        [Display(Name = "شماره رسید")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "تاریخ شمسی")]
        public string DateShamsi { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "تامین‌کننده/منبع")]
        public string SupplierOrSource { get; set; } = string.Empty;

        [Display(Name = "تامین‌کننده")]
        public int? VendorId { get; set; }

        [StringLength(600)]
        [Display(Name = "یادداشت")]
        public string? Notes { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "انبار")]
        public int WarehouseId { get; set; }

        public List<WarehouseReceiptItemVM> Items { get; set; } = new();

        public List<SelectListItem> ProductOptions { get; set; } = new();

        public List<SelectListItem> WarehouseOptions { get; set; } = new();

        public List<SelectListItem> VendorOptions { get; set; } = new();
    }

    public class WarehouseReceiptItemVM
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(0.001, 99999999999)]
        public decimal Quantity { get; set; }

        [Range(0, 99999999999)]
        public decimal UnitPrice { get; set; }
    }

    public class WarehouseIssuanceUpsertVM
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(40)]
        [Display(Name = "شماره خروج")]
        public string IssuanceNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "تاریخ شمسی")]
        public string DateShamsi { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "مقصد/دپارتمان")]
        public string DestinationOrDepartment { get; set; } = string.Empty;

        [Display(Name = "کارفرما")]
        public int? EmployerId { get; set; }

        [StringLength(600)]
        [Display(Name = "یادداشت")]
        public string? Notes { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "انبار")]
        public int WarehouseId { get; set; }

        public List<WarehouseIssuanceItemVM> Items { get; set; } = new();

        public List<SelectListItem> ProductOptions { get; set; } = new();

        public List<SelectListItem> WarehouseOptions { get; set; } = new();

        public List<SelectListItem> EmployerOptions { get; set; } = new();
    }

    public class WarehouseIssuanceItemVM
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(0.001, 99999999999)]
        public decimal Quantity { get; set; }
    }

    public class InventoryStockIndexVM
    {
        public string? SearchTerm { get; set; }

        public int? WarehouseId { get; set; }

        public List<InventoryStockRowVM> Items { get; set; } = new();
    }

    public class InventoryStockRowVM
    {
        public int ProductId { get; set; }

        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        public int WarehouseId { get; set; }

        public string WarehouseName { get; set; } = string.Empty;

        public decimal TotalInput { get; set; }

        public decimal TotalOutput { get; set; }

        public decimal CurrentQuantity { get; set; }

        public int MinimumStock { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public class InventoryCountingUpsertVM
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(40)]
        [Display(Name = "شماره سند")]
        public string DocumentNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "تاریخ شمسی")]
        public string DateShamsi { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "وضعیت")]
        public string Status { get; set; } = "Draft";

        [StringLength(600)]
        [Display(Name = "یادداشت")]
        public string? Notes { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "انبار")]
        public int WarehouseId { get; set; }

        public List<InventoryCountingItemVM> Items { get; set; } = new();

        public List<SelectListItem> ProductOptions { get; set; } = new();

        public List<SelectListItem> WarehouseOptions { get; set; } = new();
    }

    public class InventoryCountingItemVM
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(0, 99999999999)]
        public decimal SystemQuantity { get; set; }

        [Range(0, 99999999999)]
        public decimal PhysicalQuantity { get; set; }

        public decimal DiscrepancyQuantity { get; set; }
    }

    public class WarehouseClosingRequestVM
    {
        [Range(1, int.MaxValue)]
        [Display(Name = "انبار")]
        public int WarehouseId { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "تاریخ بستن")]
        public string ClosingDateShamsi { get; set; } = string.Empty;

        [Range(1300, 1700)]
        [Display(Name = "سال بستن")]
        public int ClosingYear { get; set; }
    }
}
