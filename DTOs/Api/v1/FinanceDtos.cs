using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace OfficeAutomation.DTOs.Api.v1;

/// <summary>
/// فیلتر جستجوی اسناد حسابداری
/// </summary>
public class VoucherFilterRequest
{
    /// <summary>
    /// شماره صفحه (پیش‌فرض: ۱)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// تعداد آیتم در هر صفحه (پیش‌فرض: ۲۰، حداکثر: ۱۰۰)
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// فیلتر تاریخ شروع (شمسی: yyyy/MM/dd)
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// فیلتر تاریخ پایان (شمسی: yyyy/MM/dd)
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// فیلتر بر اساس وضعیت گردش کار
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// جستجوی متنی در شماره فاکتور یا نام طرف حساب
    /// </summary>
    public string? Search { get; set; }
}

/// <summary>
/// مدل خلاصه سند حسابداری (فاکتور)
/// </summary>
public class VoucherSummaryDto
{
    /// <summary>
    /// شناسه یکتای سند
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// شماره فاکتور
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// نوع فاکتور (Sale/Purchase)
    /// </summary>
    public string InvoiceType { get; set; } = string.Empty;

    /// <summary>
    /// تاریخ شمسی
    /// </summary>
    public string DateShamsi { get; set; } = string.Empty;

    /// <summary>
    /// نام طرف حساب
    /// </summary>
    public string PartyName { get; set; } = string.Empty;

    /// <summary>
    /// مبلغ کل
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// وضعیت گردش کار
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// تاریخ ایجاد
    /// </summary>
    public DateTime CreatedAt { get; set; }

    public static VoucherSummaryDto From(Models.Invoice invoice) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        InvoiceType = invoice.InvoiceType,
        DateShamsi = invoice.DateShamsi,
        PartyName = invoice.PartyName,
        Amount = invoice.Amount,
        Status = invoice.WorkflowStatus,
        CreatedAt = invoice.CreatedAt
    };
}

/// <summary>
/// مدل جزئیات کامل یک سند حسابداری
/// </summary>
public class VoucherDetailDto
{
    /// <summary>
    /// شناسه سند
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// شماره فاکتور
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// نوع فاکتور (Sale/Purchase)
    /// </summary>
    public string InvoiceType { get; set; } = string.Empty;

    /// <summary>
    /// تاریخ شمسی
    /// </summary>
    public string DateShamsi { get; set; } = string.Empty;

    /// <summary>
    /// نام طرف حساب
    /// </summary>
    public string PartyName { get; set; } = string.Empty;

    /// <summary>
    /// کد ملی / شناسه اقتصادی
    /// </summary>
    public string? NationalCodeOrEconomicId { get; set; }

    /// <summary>
    /// جمع کل قبل از مالیات
    /// </summary>
    public decimal SubTotal { get; set; }

    /// <summary>
    /// مبلغ مالیات بر ارزش افزوده
    /// </summary>
    public decimal VatAmount { get; set; }

    /// <summary>
    /// جمع کل نهایی
    /// </summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// مبلغ فاکتور
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// وضعیت گردش کار
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// توضیحات
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// تاریخ ایجاد
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// اقلام فاکتور
    /// </summary>
    public List<VoucherItemDto> Items { get; set; } = new();

    public static VoucherDetailDto From(Models.Invoice invoice) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        InvoiceType = invoice.InvoiceType,
        DateShamsi = invoice.DateShamsi,
        PartyName = invoice.PartyName,
        NationalCodeOrEconomicId = invoice.NationalCodeOrEconomicId,
        SubTotal = invoice.SubTotal,
        VatAmount = invoice.VatAmount,
        GrandTotal = invoice.GrandTotal,
        Amount = invoice.Amount,
        Status = invoice.WorkflowStatus,
        Notes = invoice.Notes,
        CreatedAt = invoice.CreatedAt,
        Items = invoice.Items.Select(VoucherItemDto.From).ToList()
    };
}

/// <summary>
/// مدل قلم سند حسابداری
/// </summary>
public class VoucherItemDto
{
    /// <summary>
    /// نام کالا/خدمت
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// تعداد
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// قیمت واحد
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// جمع جزئی
    /// </summary>
    public decimal LineSubTotal { get; set; }

    /// <summary>
    /// مالیات این قلم
    /// </summary>
    public decimal LineVatAmount { get; set; }

    /// <summary>
    /// جمع نهایی این قلم
    /// </summary>
    public decimal LineGrandTotal { get; set; }

    public static VoucherItemDto From(Models.InvoiceItem item) => new()
    {
        ItemName = item.ItemName,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,
        LineSubTotal = item.LineSubTotal,
        LineVatAmount = item.LineVatAmount,
        LineGrandTotal = item.LineGrandTotal
    };
}

/// <summary>
/// مدل درخواست ایجاد سند حسابداری جدید
/// </summary>
public class CreateVoucherRequest
{
    /// <summary>
    /// شماره فاکتور (الزامی)
    /// </summary>
    [Required(ErrorMessage = "شماره فاکتور الزامی است.")]
    [StringLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// نوع فاکتور: Sale یا Purchase
    /// </summary>
    [Required(ErrorMessage = "نوع فاکتور الزامی است.")]
    [StringLength(20)]
    public string InvoiceType { get; set; } = "Sale";

    /// <summary>
    /// تاریخ شمسی (الزامی) - فرمت: yyyy/MM/dd
    /// </summary>
    [Required(ErrorMessage = "تاریخ شمسی الزامی است.")]
    [StringLength(20)]
    public string DateShamsi { get; set; } = string.Empty;

    /// <summary>
    /// نام طرف حساب (الزامی)
    /// </summary>
    [Required(ErrorMessage = "نام طرف حساب الزامی است.")]
    [StringLength(150)]
    public string PartyName { get; set; } = string.Empty;

    /// <summary>
    /// کد ملی / شناسه اقتصادی
    /// </summary>
    [StringLength(30)]
    public string? NationalCodeOrEconomicId { get; set; }

    /// <summary>
    /// توضیحات
    /// </summary>
    [StringLength(600)]
    public string? Notes { get; set; }

    /// <summary>
    /// مبلغ فاکتور (الزامی)
    /// </summary>
    [Required(ErrorMessage = "مبلغ فاکتور الزامی است.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "مبلغ باید بزرگتر از صفر باشد.")]
    public decimal Amount { get; set; }

    /// <summary>
    /// شناسه دریافت‌کننده انبار (برای خرید)
    /// </summary>
    public int? WarehouseReceiptId { get; set; }

    /// <summary>
    /// شناسه کارفرما (برای فروش)
    /// </summary>
    public int? EmployerId { get; set; }

    /// <summary>
    /// اقلام فاکتور
    /// </summary>
    public List<CreateVoucherItemDto> Items { get; set; } = new();
}

/// <summary>
/// مدل قلم برای ایجاد سند
/// </summary>
public class CreateVoucherItemDto
{
    /// <summary>
    /// شناسه محصول (اختیاری)
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// نام کالا/خدمت (الزامی)
    /// </summary>
    [Required(ErrorMessage = "نام کالا/خدمت الزامی است.")]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// تعداد (الزامی)
    /// </summary>
    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "تعداد باید بزرگتر از صفر باشد.")]
    public decimal Quantity { get; set; } = 1;

    /// <summary>
    /// قیمت واحد (الزامی)
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "قیمت واحد باید بزرگتر از صفر باشد.")]
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// مدل گزارش ترازنامه / خلاصه وضعیت مالی
/// </summary>
public class BalanceReportDto
{
    /// <summary>
    /// سال مالی
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// کل درآمد (فروش)
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// کل هزینه‌ها (خرید)
    /// </summary>
    public decimal TotalCosts { get; set; }

    /// <summary>
    /// سود/زیان خالص
    /// </summary>
    public decimal NetProfitLoss { get; set; }

    /// <summary>
    /// تعداد فاکتورهای فروش باز
    /// </summary>
    public int PendingSalesInvoices { get; set; }

    /// <summary>
    /// تعداد فاکتورهای خرید باز
    /// </summary>
    public int PendingPurchaseInvoices { get; set; }

    /// <summary>
    /// مالیات بر ارزش افزوده دریافت شده
    /// </summary>
    public decimal TotalVatCollected { get; set; }

    /// <summary>
    /// مالیات بر ارزش افزوده پرداخت شده
    /// </summary>
    public decimal TotalVatPaid { get; set; }
}