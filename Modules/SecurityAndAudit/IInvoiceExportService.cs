using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.SecurityAndAudit;

/// <summary>Filter for exporting invoice records.</summary>
public sealed class InvoiceExportFilter
{
    public string? InvoiceNumber { get; set; }
    public string? SellerName { get; set; }
    public string? BuyerName { get; set; }
    public string? InvoiceType { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>Exports persisted tax data to formatted Excel (.xlsx) workbooks.</summary>
public interface IInvoiceExportService
{
    /// <summary>Exports filtered invoice records to an Excel workbook.</summary>
    Task<byte[]> ExportInvoicesAsync(InvoiceExportFilter? filter, CancellationToken cancellationToken);

    /// <summary>Exports tax log breakdowns (TaxLog) to an Excel workbook.</summary>
    Task<byte[]> ExportTaxBreakdownsAsync(CancellationToken cancellationToken);

    /// <summary>Exports Mowadiyan submission reports to an Excel workbook.</summary>
    Task<byte[]> ExportMowadiyanSubmissionsAsync(CancellationToken cancellationToken);
}