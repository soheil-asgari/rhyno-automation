using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

public interface IContractGuaranteeService
{
    Task<List<ContractGuaranteeVM>> GetGuaranteesAsync(int contractId, CancellationToken ct = default);
    Task<ContractGuarantee> AddGuaranteeAsync(ContractGuaranteeCreateVM model, string currentUserId, CancellationToken ct = default);
    Task<ContractGuarantee> UpdateGuaranteeAsync(ContractGuaranteeEditVM model, string currentUserId, CancellationToken ct = default);
    Task<bool> ReleaseGuaranteeAsync(int id, string currentUserId, CancellationToken ct = default);
    Task<bool> DeleteGuaranteeAsync(int id, string currentUserId, CancellationToken ct = default);
    /// <summary>
    /// Returns guarantees that are Active and expiring within the next 30 days (for alert engine).
    /// </summary>
    Task<List<ContractGuarantee>> GetExpiringGuaranteesAsync(CancellationToken ct = default);
    /// <summary>
    /// Updates guarantee statuses: Active→Expiring (30 days before expiry), Expiring→Expired (past expiry).
    /// </summary>
    Task<int> UpdateExpiringGuaranteeStatusesAsync(CancellationToken ct = default);
}