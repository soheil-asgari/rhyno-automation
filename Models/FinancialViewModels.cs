using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models
{
    public class FinancialInvoiceIndexVM
    {
        public string? SearchTerm { get; set; }

        public string? InvoiceType { get; set; }

        public int? Year { get; set; }

        public int? Quarter { get; set; }

        public List<Invoice> Items { get; set; } = new();
    }

    public class FinancialInvoiceUpsertVM
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "شماره فاکتور")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "نوع فاکتور")]
        public string InvoiceType { get; set; } = "Sale";

        [Required]
        [StringLength(20)]
        [Display(Name = "تاریخ شمسی")]
        public string DateShamsi { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [Display(Name = "نام طرف معامله")]
        public string PartyName { get; set; } = string.Empty;

        [StringLength(30)]
        [Display(Name = "کد ملی / شناسه اقتصادی")]
        public string? NationalCodeOrEconomicId { get; set; }

        [Display(Name = "کارفرما")]
        public int? EmployerId { get; set; }

        [Display(Name = "جمع جزء")]
        public decimal SubTotal { get; set; }

        [Display(Name = "مالیات ارزش افزوده 10٪")]
        public decimal VatAmount { get; set; }

        [Display(Name = "جمع کل")]
        public decimal GrandTotal { get; set; }

        [StringLength(600)]
        [Display(Name = "یادداشت")]
        public string? Notes { get; set; }

        [Display(Name = "رسید انبار مرتبط")]
        public int? WarehouseReceiptId { get; set; }

        [Display(Name = "مسئول پیگیری")]
        public int? FollowUpEmployeeId { get; set; }

        [StringLength(20)]
        [Display(Name = "مهلت پیگیری (شمسی)")]
        public string? DeadlineDateShamsi { get; set; }

        public List<FinancialInvoiceItemVM> Items { get; set; } = new();

        public List<SelectListItem> ProductOptions { get; set; } = new();

        public List<SelectListItem> WarehouseReceiptOptions { get; set; } = new();

        public List<SelectListItem> FollowUpEmployeeOptions { get; set; } = new();

        public List<SelectListItem> EmployerOptions { get; set; } = new();
    }

    public class FinancialInvoiceItemVM
    {
        public int? ProductId { get; set; }

        [Required]
        [StringLength(150)]
        public string ItemName { get; set; } = string.Empty;

        [Range(0.001, 99999999999)]
        public decimal Quantity { get; set; }

        [Range(0, 99999999999)]
        public decimal UnitPrice { get; set; }

        public decimal LineSubTotal { get; set; }

        public decimal LineVatAmount { get; set; }

        public decimal LineGrandTotal { get; set; }
    }

    public class FinancialDashboardVM
    {
        public int PendingSalesInvoices { get; set; }

        public int PendingPurchaseInvoices { get; set; }

        public int DuePurchaseDeadlines { get; set; }

        public int ValidationWarnings { get; set; }

        public List<FinancialQuickActionVM> QuickActions { get; set; } = new();

        public List<FinancialHubKpiVM> HubKpis { get; set; } = new();

        public List<FinancialActivityVM> RecentActivities { get; set; } = new();

        public decimal TotalRevenue { get; set; }

        public decimal TotalPurchaseCost { get; set; }

        public decimal TotalPayrollCost { get; set; }

        public decimal TotalCosts { get; set; }

        public decimal NetProfitLoss { get; set; }

        public List<string> MonthLabels { get; set; } = new();

        public List<decimal> RevenueSeries { get; set; } = new();

        public List<decimal> CostSeries { get; set; } = new();

        public List<decimal> ProfitSeries { get; set; } = new();

        public decimal TotalVatCollected { get; set; }

        public decimal TotalVatPaid { get; set; }

        public decimal NetVatPayableOrRefundable { get; set; }

        public decimal TotalVatLineCalculated { get; set; }

        public decimal VatReconciliationDifference { get; set; }

        public int VatMismatchInvoices { get; set; }

        public int SeasonalMismatchCount { get; set; }

        public int OverdueInvoices { get; set; }

        public decimal MonthlySalesTotal { get; set; }

        public decimal MonthlyPurchaseTotal { get; set; }

        public decimal MonthlyTaxPayable { get; set; }

        public List<VatInvoiceRowVM> VatRows { get; set; } = new();
    }

    public class FinancialQuickActionVM
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Url { get; set; } = "#";

        public string Icon { get; set; } = "bi-arrow-right-circle";

        public string Tone { get; set; } = "primary";
    }

    public class FinancialHubKpiVM
    {
        public string Title { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Tone { get; set; } = "primary";
    }

    public class FinancialActivityVM
    {
        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string Url { get; set; } = "#";

        public string Badge { get; set; } = string.Empty;

        public string BadgeTone { get; set; } = "secondary";
    }

    public class VatInvoiceRowVM
    {
        public int Id { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public string InvoiceType { get; set; } = string.Empty;

        public string DateShamsi { get; set; } = string.Empty;

        public string PartyName { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }

        public decimal VatAmount { get; set; }

        public decimal GrandTotal { get; set; }
    }

    public class SeasonalTaxReportVM
    {
        public int Year { get; set; }

        public int Quarter { get; set; }

        public string QuarterTitle { get; set; } = string.Empty;

        public List<SeasonalTaxReportRowVM> Rows { get; set; } = new();

        public decimal TotalAmount { get; set; }

        public decimal TotalVat { get; set; }
    }

    public class SeasonalTaxReportRowVM
    {
        public string PartyName { get; set; } = string.Empty;

        public string? NationalId { get; set; }

        public string TransactionType { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal Vat { get; set; }

        public string DateShamsi { get; set; } = string.Empty;
    }
}
