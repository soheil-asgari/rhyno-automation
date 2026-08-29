using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

public interface IContractService
{
    // ── Contract CRUD ─────────────────────────────────────────────
    Task<ContractIndexVM> GetIndexViewModelAsync(ContractIndexVM filter, string currentUserId, CancellationToken ct = default);
    Task<Contract?> GetContractByIdAsync(int id, string currentUserId, CancellationToken ct = default);
    Task<ContractDetailsVM> GetDetailsViewModelAsync(int id, string currentUserId, CancellationToken ct = default);
    Task<ContractCreateVM> BuildCreateViewModelAsync(string currentUserId, CancellationToken ct = default);
    Task<Contract> CreateContractAsync(ContractCreateVM model, string currentUserId, CancellationToken ct = default);
    Task<ContractEditVM> BuildEditViewModelAsync(int id, string currentUserId, CancellationToken ct = default);
    Task<Contract> UpdateContractAsync(ContractEditVM model, string currentUserId, CancellationToken ct = default);
    Task<bool> DeleteContractAsync(int id, string currentUserId, CancellationToken ct = default);

    // ── Archive ───────────────────────────────────────────────────
    Task<ContractArchiveVM> GetArchiveViewModelAsync(ContractArchiveVM filter, string currentUserId, CancellationToken ct = default);
    Task<bool> ArchiveContractAsync(int id, string currentUserId, CancellationToken ct = default);
    Task<bool> UnarchiveContractAsync(int id, string currentUserId, CancellationToken ct = default);

    // ── Payments ──────────────────────────────────────────────────
    Task<ContractPayment> AddPaymentAsync(ContractPaymentCreateVM model, string currentUserId, CancellationToken ct = default);
    Task<bool> DeletePaymentAsync(int paymentId, int contractId, string currentUserId, CancellationToken ct = default);

    // ── Attachments ───────────────────────────────────────────────
    Task<ContractAttachment> UploadAttachmentAsync(int contractId, string fileName, Stream fileStream, string contentType, string uploadedByUserId, CancellationToken ct = default);
    Task<ContractAttachment?> GetAttachmentAsync(int attachmentId, string currentUserId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(int attachmentId, int contractId, string currentUserId, CancellationToken ct = default);
    /// <summary>Deletes an attachment by Id only, resolving contract from the attachment record.</summary>
    Task<bool> DeleteAttachmentByIdOnlyAsync(int attachmentId, string currentUserId, CancellationToken ct = default);
    /// <summary>
    /// Resolves the physical path for an attachment after verifying the caller
    /// has access to the parent contract. Returns null if unauthorized or missing.
    /// </summary>
    Task<(ContractAttachment? Attachment, string? FullPath)> GetAttachmentForDownloadAsync(int attachmentId, string currentUserId, CancellationToken ct = default);
    /// <summary>
    /// Validates a file upload (size, extension, MIME, magic bytes) and previews the
    /// storage path safely.
    /// </summary>
    ContractAttachment? ValidateAttachmentUpload(string fileName, string contentType, long sizeBytes);
    /// <summary>
    /// Adds an audit log entry for a contract.
    /// </summary>
    Task AddAuditLogAsync(int contractId, string userId, string action, string? oldStatus, string? newStatus, string? description = null, CancellationToken ct = default);
    /// <summary>Removes orphaned attachment DB records whose physical files are missing. Admin only.</summary>
    Task<int> CleanupOrphanedAttachmentsAsync(int? contractId, string currentUserId, CancellationToken ct = default);

    // ── Templates / Settings ──────────────────────────────────────
    Task<ContractSettingsVM> GetSettingsViewModelAsync(string currentUserId, CancellationToken ct = default);
    Task<ContractTemplate> SaveTemplateAsync(ContractSettingsVM model, string currentUserId, CancellationToken ct = default);
    Task<ContractUserSetting> SaveSettingsAsync(ContractSettingsVM model, string currentUserId, CancellationToken ct = default);
    Task<bool> DeleteTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default);
    Task<bool> SetDefaultTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default);
    Task<List<ContractTemplate>> GetUserTemplatesAsync(string currentUserId, CancellationToken ct = default);

    // ── Document Generation & Template Rendering ──────────────────
    Task<string> GenerateContractDocumentAsync(int contractId, int? templateId, string currentUserId, CancellationToken ct = default);
    Task<string> PreviewTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default);

    // ── Dashboard ─────────────────────────────────────────────────
    Task<ContractDashboardVM> GetDashboardAsync(string currentUserId, CancellationToken ct = default);

    // ── Customers ─────────────────────────────────────────────────
    Task<List<Customer>> GetCustomersAsync(CancellationToken ct = default);
    Task<Customer?> GetCustomerByIdAsync(int id, CancellationToken ct = default);
    Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken ct = default);
    Task<Customer> UpdateCustomerAsync(Customer customer, CancellationToken ct = default);

    // ── Authorization & RLS ───────────────────────────────────────
    bool CanUserViewContract(Contract contract, string currentUserId, bool isAdmin);
    bool CanUserEditContract(Contract contract, string currentUserId, bool isAdmin);
    bool CanUserViewFinancials(Contract contract, string currentUserId, bool isAdmin);
    Task EnforceAccessAsync(int contractId, string currentUserId, CancellationToken ct = default);

    // ── Validation ────────────────────────────────────────────────
    Task<bool> IsContractNumberUniqueAsync(string contractNumber, int? excludeId = null, CancellationToken ct = default);
    Task<string> GenerateNextContractNumberAsync(CancellationToken ct = default);

    // ── Dynamic Form Fields (EAV) ─────────────────────────────────
    /// <summary>Load all type field definitions for all contract types</summary>
    Task<List<ContractTypeFieldVM>> GetAllTypeFieldDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Save field values for a contract (replaces all existing values)</summary>
    Task SaveFieldValuesAsync(int contractId, string contractType, Dictionary<string, string> fieldValues, CancellationToken ct = default);

    /// <summary>Load field values for a contract, matched with their definitions</summary>
    Task<List<ContractTypeFieldVM>> LoadFieldValuesAsync(int contractId, CancellationToken ct = default);

    // ── Financial Tracking ────────────────────────────────────────
    Task RecalculatePaymentStatusAsync(int contractId, CancellationToken ct = default);

/// <summary>
    /// Backfill: ایجاد سند درآمد معوق (Deferred Revenue) برای قراردادهای فعالی که
    /// پیش از اضافه شدن این قابلیت ایجاد شده بودند. یک‌بار اجرا می‌شود.
    /// </summary>
    /// <returns>تعداد قراردادهای به‌روز شده، و تعداد خطاها</returns>
    Task<(int Updated, int Errors)> BackfillDeferredRevenueForExistingAsync(string currentUserId, CancellationToken ct = default);
    // ── Expiry Engine ─────────────────────────────────────────────
    Task<int> UpdateExpiringSoonStatusesAsync(CancellationToken ct = default);

    // ── Bulk Actions ──────────────────────────────────────────────
    /// <summary>
    /// Executes a bulk action (status change, assign, archive, delete) on
    /// multiple contracts in a single transaction. All-or-nothing.
    /// </summary>
    Task<BulkActionResult> BulkActionAsync(BulkActionRequest request, string currentUserId, CancellationToken ct = default);

    // ── Auto-Renewal ───────────────────────────────────────────
    /// <summary>
    /// Processes all contracts that are due for auto-renewal.
    /// Creates a new contract period and logs the event.
    /// Should be called from the background job coordinator.
    /// </summary>
    /// <returns>Number of contracts renewed successfully</returns>
    Task<int> ProcessAutoRenewalsAsync(CancellationToken ct = default);

    /// <summary>
    /// Cancels the auto-renewal for a contract before it executes.
    /// </summary>
    Task<bool> CancelAutoRenewalAsync(int contractId, string currentUserId, CancellationToken ct = default);

    // ── Column Customization ───────────────────────────────────────
    /// <summary>
    /// Saves the user's column visibility preferences for the contracts list.
    /// </summary>
    Task SaveColumnPreferenceAsync(string userId, string[] columnNames, CancellationToken ct = default);

    /// <summary>
    /// Gets the user's saved column names for the contracts list.
    /// Returns null if no preference has been saved.
    /// </summary>
    Task<string[]?> GetColumnPreferenceAsync(string userId, CancellationToken ct = default);
}