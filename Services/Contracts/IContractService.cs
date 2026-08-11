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
    Task<ContractPayment> AddPaymentAsync(ContractPaymentCreateVM model, CancellationToken ct = default);
    Task<bool> DeletePaymentAsync(int paymentId, int contractId, CancellationToken ct = default);

    // ── Attachments ───────────────────────────────────────────────
    Task<ContractAttachment> UploadAttachmentAsync(int contractId, string fileName, Stream fileStream, string contentType, string uploadedByUserId, CancellationToken ct = default);
    Task<ContractAttachment?> GetAttachmentAsync(int attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(int attachmentId, int contractId, CancellationToken ct = default);

    // ── Templates / Settings ──────────────────────────────────────
    Task<ContractSettingsVM> GetSettingsViewModelAsync(string currentUserId, CancellationToken ct = default);
    Task<ContractTemplate> SaveTemplateAsync(ContractSettingsVM model, string currentUserId, CancellationToken ct = default);
    Task<ContractUserSetting> SaveSettingsAsync(ContractSettingsVM model, string currentUserId, CancellationToken ct = default);
    Task<bool> DeleteTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default);
    Task<bool> SetDefaultTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default);
    Task<List<ContractTemplate>> GetUserTemplatesAsync(string currentUserId, CancellationToken ct = default);

    // ── Document Generation ───────────────────────────────────────
    Task<string> GenerateContractDocumentAsync(int contractId, int? templateId, string currentUserId, CancellationToken ct = default);

    // ── Dashboard ─────────────────────────────────────────────────
    Task<ContractDashboardVM> GetDashboardAsync(string currentUserId, CancellationToken ct = default);

    // ── Customers ─────────────────────────────────────────────────
    Task<List<Customer>> GetCustomersAsync(CancellationToken ct = default);
    Task<Customer?> GetCustomerByIdAsync(int id, CancellationToken ct = default);
    Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken ct = default);
    Task<Customer> UpdateCustomerAsync(Customer customer, CancellationToken ct = default);

    // ── Authorization ─────────────────────────────────────────────
    bool CanUserViewContract(Contract contract, string currentUserId, bool isAdmin);
    bool CanUserEditContract(Contract contract, string currentUserId, bool isAdmin);
    bool CanUserViewFinancials(Contract contract, string currentUserId, bool isAdmin);

    // ── Validation ────────────────────────────────────────────────
    Task<bool> IsContractNumberUniqueAsync(string contractNumber, int? excludeId = null, CancellationToken ct = default);
}