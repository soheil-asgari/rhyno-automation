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
}