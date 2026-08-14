using Microsoft.AspNetCore.Mvc.Rendering;
using OfficeAutomation.Modules.Commercial.Domain;

namespace OfficeAutomation.Modules.Commercial.Application;

/// <summary>
/// Payment status of a commercial invoice, derived from actual posted settlement
/// transactions and approved returns. It is never stored denormalized.
/// </summary>
public static class CommercialPaymentStatus
{
    public const string Unpaid = "Unpaid";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string FullyPaid = "FullyPaid";
    public const string Overpaid = "Overpaid";
    public const string Cancelled = "Cancelled";

    public static string Label(string? status)
    {
        return status switch
        {
            Unpaid => "پرداخت نشده",
            PartiallyPaid => "پرداخت جزئی",
            FullyPaid => "پرداخت کامل",
            Overpaid => "اضافه پرداخت",
            Cancelled => "ابطال شده",
            _ => status ?? "-"
        };
    }

    public static string BadgeCss(string? status)
    {
        return status switch
        {
            FullyPaid => "bg-emerald-50 text-emerald-700 border-emerald-200/60 dark:bg-emerald-950/40 dark:text-emerald-400 dark:border-emerald-800/40",
            PartiallyPaid => "bg-amber-50 text-amber-700 border-amber-200/60 dark:bg-amber-950/40 dark:text-amber-400 dark:border-amber-800/40",
            Overpaid => "bg-violet-50 text-violet-700 border-violet-200/60 dark:bg-violet-950/40 dark:text-violet-400 dark:border-violet-800/40",
            Cancelled => "bg-rose-50 text-rose-700 border-rose-200/60 dark:bg-rose-950/40 dark:text-rose-400 dark:border-rose-800/40",
            _ => "bg-slate-100 text-slate-600 border-slate-200/60 dark:bg-slate-800/40 dark:text-slate-400 dark:border-slate-700/40"
        };
    }

    public static string Resolve(decimal total, decimal paid, decimal returned, bool cancelled)
    {
        if (cancelled)
        {
            return Cancelled;
        }

        if (total <= 0)
        {
            return FullyPaid;
        }

        var outstanding = total - returned - paid;
        if (outstanding > 0 && paid == 0 && returned == 0)
        {
            return Unpaid;
        }

        if (outstanding < 0)
        {
            return Overpaid;
        }

        return outstanding == 0 ? FullyPaid : PartiallyPaid;
    }
}

public sealed class CommercialInvoiceRowVM
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceType { get; set; } = "Sale";
    public string DateShamsi { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string DueDateShamsi { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ReturnedAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string? VoucherNumber { get; set; }
    public string WorkflowStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = CommercialPaymentStatus.Unpaid;
    public int AttachmentCount { get; set; }
}

public sealed class CommercialInvoiceIndexVM
{
    public string? SearchTerm { get; set; }
    public int? Year { get; set; }
    public string? PartyNameFilter { get; set; }
    public string? StatusFilter { get; set; }
    public string? PaymentStatusFilter { get; set; }
    public string InvoiceType { get; set; } = "Sale";
    public List<CommercialInvoiceRowVM> Items { get; set; } = new();
    public decimal TotalPayable { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
}

public sealed class PartyRowVM
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CommercialPartyType PartyType { get; set; }
    public string PartyTypeLabel { get; set; } = string.Empty;
    public string? EconomicCode { get; set; }
    public string? NationalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public decimal Balance { get; set; }
}

public sealed class PartyUpsertVM
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CommercialPartyType PartyType { get; set; } = CommercialPartyType.Both;
    public string? NationalCode { get; set; }
    public string? EconomicCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class InvoiceReturnRowVM
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string ReturnType { get; set; } = "Sale";
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string DateShamsi { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? VoucherNumber { get; set; }
    public string? Reason { get; set; }
}

public sealed class InvoiceReturnIndexVM
{
    public string ReturnType { get; set; } = "Sale";
    public string? SearchTerm { get; set; }
    public List<InvoiceReturnRowVM> Items { get; set; } = new();
}

public sealed class QuotationRowVM
{
    public int Id { get; set; }
    public string QuotationNumber { get; set; } = string.Empty;
    public string QuotationType { get; set; } = "Sale";
    public string PartyName { get; set; } = string.Empty;
    public string DateShamsi { get; set; } = string.Empty;
    public string? ValidUntilShamsi { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ConvertedInvoiceId { get; set; }
}

public sealed class QuotationIndexVM
{
    public string QuotationType { get; set; } = "Sale";
    public string? SearchTerm { get; set; }
    public List<QuotationRowVM> Items { get; set; } = new();
}

public sealed class SettlementRowVM
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string DateShamsi { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? VoucherNumber { get; set; }
}

public sealed class InvoiceDetailVM
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceType { get; set; } = "Sale";
    public string PartyName { get; set; } = string.Empty;
    public string DateShamsi { get; set; } = string.Empty;
    public string? DueDateShamsi { get; set; }
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public string WorkflowStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = CommercialPaymentStatus.Unpaid;
    public decimal PaidAmount { get; set; }
    public decimal ReturnedAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string? VoucherNumber { get; set; }
    public List<SettlementRowVM> Settlements { get; set; } = new();
    public List<InvoiceAttachmentVM> Attachments { get; set; } = new();
}

public sealed class InvoiceAttachmentVM
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public InvoiceAttachmentCategory Category { get; set; }
    public string CategoryLabel { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

public static class InvoiceAttachmentCategoryLabels
{
    public static string Label(InvoiceAttachmentCategory category)
    {
        return category switch
        {
            InvoiceAttachmentCategory.InvoiceScan => "اسکن فاکتور",
            InvoiceAttachmentCategory.Contract => "قرارداد",
            InvoiceAttachmentCategory.Receipt => "رسید",
            _ => "مدرک پشتیبان"
        };
    }
}