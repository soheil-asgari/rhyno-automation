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

        public List<WarehouseLowStockItemVM> CriticalStocks { get; set; } = new();

        public List<WarehouseRecentDocumentVM> RecentReceipts { get; set; } = new();

        public List<WarehouseRecentDocumentVM> RecentIssuances { get; set; } = new();

        public List<WarehousePendingTransferVM> PendingTransfers { get; set; } = new();

        public List<WarehouseRiskWarehouseVM> RiskWarehouses { get; set; } = new();

        public List<WarehouseCountingIssueVM> CountingIssues { get; set; } = new();

        public List<WarehouseMovementTrendVM> MovementTrends { get; set; } = new();
    }

    public class WarehouseLowStockItemVM
    {
        public int WarehouseId { get; set; }

        public string WarehouseName { get; set; } = string.Empty;

        public int ProductId { get; set; }

        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public decimal CurrentQuantity { get; set; }

        public int MinimumStock { get; set; }


        public decimal AverageCost { get; set; }

        public decimal InventoryValue { get; set; }

        public decimal WeightedCost { get; set; }
    }

    public class WarehouseRecentDocumentVM
    {
        public int Id { get; set; }

        public string Number { get; set; } = string.Empty;

        public string DateShamsi { get; set; } = string.Empty;

        public string WarehouseName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public decimal TotalQuantity { get; set; }
    }

    public class WarehousePendingTransferVM
    {
        public int Id { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string SourceWarehouseName { get; set; } = string.Empty;

        public string DestinationWarehouseName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public string Status { get; set; } = string.Empty;
    }

    public class WarehouseRiskWarehouseVM
    {
        public int WarehouseId { get; set; }

        public string WarehouseName { get; set; } = string.Empty;

        public int OpenDocuments { get; set; }

        public int LowStockCount { get; set; }

        public int NegativeStockCount { get; set; }
    }

    public class WarehouseCountingIssueVM
    {
        public int Id { get; set; }

        public string DocumentNumber { get; set; } = string.Empty;

        public string WarehouseName { get; set; } = string.Empty;

        public decimal TotalDiscrepancy { get; set; }

        public string Status { get; set; } = string.Empty;
    }

    public class WarehouseMovementTrendVM
    {
        public string Label { get; set; } = string.Empty;

        public decimal Inputs { get; set; }

        public decimal Outputs { get; set; }
    }

    public class WarehouseStockSnapshotVM
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string ProductCode { get; set; } = string.Empty;

        public decimal CurrentQuantity { get; set; }

        public int MinimumStock { get; set; }


        public decimal AverageCost { get; set; }

        public decimal InventoryValue { get; set; }

        public decimal WeightedCost { get; set; }
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

        [StringLength(80)]
        [Display(Name = "ط¯ط³طھظ‡")]
        public string? Category { get; set; }

        [StringLength(50)]
        [Display(Name = "ط¨ط§ط±ع©ط¯")]
        public string? Barcode { get; set; }

        [StringLength(200)]
        [Display(Name = "طھظˆط¶غŒط­ ظپظ†غŒ")]
        public string? TechnicalDescription { get; set; }

        [Display(Name = "ظ‚ط§ط¨ظ„ ط®ط±غŒط¯")]
        public bool IsPurchasable { get; set; } = true;

        [Display(Name = "ظ‚ط§ط¨ظ„ طµط±ظپ")]
        public bool IsConsumable { get; set; } = true;

        [StringLength(30)]
        [Display(Name = "ظˆط§ط­ط¯ ع¯ط²غŒظ†")]
        public string? SecondaryUnit { get; set; }

        [Range(0, 999999)]
        [Display(Name = "ظ†ظ‚ط·ظ‡ ط³ظپط§ط±ط´")]
        public decimal ReorderPoint { get; set; }

        [Range(0, 999999)]
        [Display(Name = "ط­ط¯ط§ع©ط«ط±")]
        public decimal MaximumStock { get; set; }

        [Display(Name = "ط¢ط®ط±غŒظ† ظ‚غŒظ…طھ")]
        public decimal? LastPurchasePrice { get; set; }

        [Range(0, 999999)]
        [Display(Name = "حداقل موجودی")]
        public int MinimumStock { get; set; }


        public decimal AverageCost { get; set; }

        public decimal InventoryValue { get; set; }

        public decimal WeightedCost { get; set; }

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
        [StringLength(50)]
        [Display(Name = "ظ†ظˆط¹ ط§ظ†ط¨ط§ط±")]
        public string? WarehouseType { get; set; }

        [Range(0, 999999)]
        [Display(Name = "ظˆط³ط¹طھ")]
        public decimal Capacity { get; set; }

        [Display(Name = "مدیر انبار")]
        public string? ManagerUserId { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "بسته")]
        public bool IsClosed { get; set; }
        [StringLength(600)]
        [Display(Name = "ظ‚ظˆط§ظ†غŒظ† ط¨ط³طھظ†")]
        public string? ClosingRules { get; set; }

        public List<SelectListItem> ManagerOptions { get; set; } = new();
    }

    public class WarehouseReceiptUpsertVM
    {
        public int? Id { get; set; }

        [Display(Name = "ذخیره به صورت پیش نویس")]
        public bool SaveAsDraft { get; set; }

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

        [Display(Name = "ذخیره به صورت پیش نویس")]
        public bool SaveAsDraft { get; set; }

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

        public bool CriticalOnly { get; set; }

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

        public decimal AverageCost { get; set; }

        public decimal LastPurchasePrice { get; set; }

        public decimal InventoryValue { get; set; }

        public decimal WeightedCost { get; set; }

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

        public List<WarehouseClosingPreflightItemVM> PreflightItems { get; set; } = new();

        public bool CanClose { get; set; }
    }

    public class WarehouseClosingPreflightItemVM
    {
        public string Key { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public bool IsBlocking { get; set; }
    }

    public class WarehouseAuditEntryVM
    {
        public string Action { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public DateTimeOffset OccurredAt { get; set; }

        public string? ActorName { get; set; }
    }

    public class WarehouseMovementEntryVM
    {
        public string ProductName { get; set; } = string.Empty;

        public string ProductCode { get; set; } = string.Empty;

        public string WarehouseName { get; set; } = string.Empty;

        public decimal QuantityIn { get; set; }

        public decimal QuantityOut { get; set; }

        public decimal BalanceAfter { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? ActorName { get; set; }
    }

    public class WarehouseReceiptDetailsVM
    {
        public WarehouseReceipt Receipt { get; set; } = null!;

        public decimal TotalQuantity { get; set; }

        public decimal TotalAmount { get; set; }

        public List<WarehouseAuditEntryVM> AuditEntries { get; set; } = new();

        public List<WarehouseMovementEntryVM> MovementEntries { get; set; } = new();
    }

    public class WarehouseIssuanceDetailsVM
    {
        public WarehouseIssuance Issuance { get; set; } = null!;

        public decimal TotalQuantity { get; set; }

        public List<WarehouseAuditEntryVM> AuditEntries { get; set; } = new();

        public List<WarehouseMovementEntryVM> MovementEntries { get; set; } = new();
    }

    public class WarehouseTransferDetailsVM
    {
        public InventoryTransferRequest Request { get; set; } = null!;

        public List<WarehouseAuditEntryVM> AuditEntries { get; set; } = new();

        public List<WarehouseMovementEntryVM> MovementEntries { get; set; } = new();
    }

    public class WarehouseCountingDetailsVM
    {
        public InventoryCounting Counting { get; set; } = null!;

        public List<WarehouseAuditEntryVM> AuditEntries { get; set; } = new();

        public List<WarehouseMovementEntryVM> MovementEntries { get; set; } = new();
    }
}
