using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس تایید قراردادها — مدیریت درخواست‌های تایید چندمرحله‌ای، موازی، آستانه‌ای و ارجاع
/// </summary>
public interface IApprovalService
{
    // ── Approval Request CRUD ──────────────────────────────────────

    /// <summary>
    /// ایجاد یک درخواست تایید جدید برای یک قرارداد.
    /// مراحل تایید به صورت خودکار بر اساس مبلغ و نوع قرارداد ساخته می‌شوند.
    /// </summary>
    Task<ContractApprovalRequest> CreateApprovalRequestAsync(
        int contractId, string currentUserId,
        string? description = null, CancellationToken ct = default);

    /// <summary>
    /// دریافت جزئیات یک درخواست تایید به همراه مراحل آن
    /// </summary>
    Task<ContractApprovalRequest?> GetApprovalRequestByIdAsync(
        int requestId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// دریافت درخواست تایید فعال یک قرارداد (در وضعیت Pending یا PartiallyApproved)
    /// </summary>
    Task<ContractApprovalRequest?> GetActiveApprovalRequestForContractAsync(
        int contractId, CancellationToken ct = default);

    /// <summary>
    /// لیست درخواست‌های تایید برای یک قرارداد
    /// </summary>
    Task<List<ContractApprovalRequest>> GetApprovalRequestsForContractAsync(
        int contractId, CancellationToken ct = default);

    /// <summary>
    /// لیست درخواست‌های تایید منتظر اقدام توسط کاربر جاری
    /// </summary>
    Task<List<ContractApprovalRequest>> GetPendingApprovalsForUserAsync(
        string userId, CancellationToken ct = default);

    /// <summary>
    /// لغو یک درخواست تایید (فقط توسط درخواست‌کننده یا ادمین)
    /// </summary>
    Task<bool> CancelApprovalRequestAsync(
        int requestId, string currentUserId, CancellationToken ct = default);

    // ── Approval Actions ───────────────────────────────────────────

    /// <summary>
    /// تایید یک مرحله از درخواست.
    /// در حالت Sequential: اگر همه مراحل تایید شوند، درخواست Approved می‌شود.
    /// در حالت Parallel: تا زمانی که همه مراحل جاری تایید شوند صبر می‌کند.
    /// </summary>
    Task<ContractApprovalStep> ApproveStepAsync(
        int stepId, string currentUserId,
        string? comment = null, CancellationToken ct = default);

    /// <summary>
    /// رد یک مرحله از درخواست.
    /// در حالت Sequential: درخواست به وضعیت Rejected می‌رود و طبق RejectionReturnMode عمل می‌شود.
    /// در حالت Parallel: رد یکی از مراحل موازی باعث رد کل درخواست می‌شود.
    /// </summary>
    Task<ContractApprovalStep> RejectStepAsync(
        int stepId, string currentUserId,
        string comment, string returnMode = "Draft",
        CancellationToken ct = default);

    /// <summary>
    /// ارجاع یک مرحله به کاربر دیگر (Delegation) زمانی که تاییدکننده اصلی در دسترس نیست.
    /// </summary>
    Task<ContractApprovalStep> DelegateStepAsync(
        int stepId, string currentUserId,
        string delegateToUserId, string? reason = null,
        CancellationToken ct = default);

    // ── Threshold / Auto-Routing ───────────────────────────────────

    /// <summary>
    /// تعیین مسیر تایید بر اساس مبلغ قرارداد.
    /// Manager < $10K, Director < $100K, VP >= $100K
    /// </summary>
    Task<List<ContractApprovalStep>> BuildApprovalStepsForAmountAsync(
        int requestId, decimal amount, string? contractType = null,
        CancellationToken ct = default);

    // ── Delegation Management ──────────────────────────────────────

    /// <summary>
    /// ایجاد یک ارجاع تایید (delegation) از یک کاربر به کاربر دیگر
    /// </summary>
    Task<ContractApprovalDelegation> CreateDelegationAsync(
        string fromUserId, string toUserId,
        DateTime startsAt, DateTime endsAt,
        string? contractType = null, string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// لغو یک ارجاع تایید
    /// </summary>
    Task<bool> CancelDelegationAsync(
        int delegationId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// دریافت ارجاع‌های فعال برای یک کاربر
    /// </summary>
    Task<List<ContractApprovalDelegation>> GetActiveDelegationsForUserAsync(
        string userId, CancellationToken ct = default);

    /// <summary>
    /// بررسی می‌کند که آیا کاربر اصلی برای این مرحله delegation فعال دارد یا خیر
    /// و در صورت وجود، کاربر جایگزین را برمی‌گرداند.
    /// </summary>
    Task<string?> ResolveDelegationAsync(
        string assignedUserId, string? contractType = null,
        CancellationToken ct = default);
}