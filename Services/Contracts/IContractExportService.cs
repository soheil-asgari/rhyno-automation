using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// Service for exporting contracts and reports to Excel/PDF.
/// Uses ClosedXML (Excel) and Playwright (PDF).
/// </summary>
public interface IContractExportService
{
    /// <summary>
    /// Export filtered contract list to Excel (.xlsx).
    /// Columns: ContractNumber, Title, ContractType, Status, Value, StartDate, EndDate, AssignedTo, CreatedDate
    /// </summary>
    Task<byte[]> ExportContractsToExcelAsync(ContractIndexVM filters, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Export a specific report to Excel (.xlsx).
    /// reportType: "Aging", "Revenue", "Pipeline", "Calendar", "Exposure"
    /// </summary>
    Task<byte[]> ExportReportToExcelAsync(string reportType, CancellationToken ct = default);

    /// <summary>
    /// Export a specific report to PDF using Playwright (HTML → PDF).
    /// reportType: "Aging", "Revenue", "Pipeline", "Calendar", "Exposure"
    /// Returns byte array of PDF content.
    /// </summary>
    Task<byte[]> ExportReportToPdfAsync(string reportType, CancellationToken ct = default);

    /// <summary>
    /// Export filtered contract list to PDF via Playwright (HTML → PDF).
    /// Columns: ContractNumber, Title, ContractType, Status, Value, StartDate, EndDate, AssignedTo, CreatedDate
    /// </summary>
    Task<byte[]> ExportContractsToPdfAsync(ContractIndexVM filters, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Export a generated contract document to DOCX (.docx) format.
    /// </summary>
    Task<byte[]> ExportContractToDocxAsync(int contractId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Generate HTML table markup for a given report type (used by PDF export).
    /// </summary>
    Task<string> GenerateReportHtmlAsync(string reportType, CancellationToken ct = default);
}