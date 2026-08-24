using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس مدیریت اصلاحیه‌ها / الحاقیه‌ها / تمدیدهای قرارداد
/// </summary>
public interface IAmendmentService
{
    /// <summary>
    /// دریافت لیست اصلاحیه‌های یک قرارداد
    /// </summary>
    Task<List<AmendmentListItemVM>> GetAmendmentsForContractAsync(
        int contractId, CancellationToken ct = default);

    /// <summary>
    /// دریافت جزئیات یک اصلاحیه
    /// </summary>
    Task<AmendmentDetailVM?> GetAmendmentDetailAsync(
        int amendmentId, CancellationToken ct = default);

    /// <summary>
    /// ایجاد اصلاحیه جدید (وضعیت: Draft)
    /// </summary>
    Task<ContractAmendment> CreateAmendmentAsync(
        AmendmentCreateVM model, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// ویرایش اصلاحیه (فقط در وضعیت Draft)
    /// </summary>
    Task<ContractAmendment?> UpdateAmendmentAsync(
        int amendmentId, AmendmentEditVM model, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// ارسال به گردش تایید (Draft → PendingApproval)
    /// از IApprovalService استفاده می‌کند
    /// </summary>
    Task<ContractAmendment?> SubmitForApprovalAsync(
        int amendmentId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// تایید نهایی اصلاحیه توسط گردش تایید (وقتی ApprovalRequest Approved شود)
    /// </summary>
    Task<ContractAmendment?> ApproveAmendmentAsync(
        int amendmentId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// اعمال اصلاحیه روی قرارداد (Approved → Applied)
    /// - مبلغ و تاریخ پایان قرارداد را به‌روز می‌کند
    /// - اسنپشات نسخه (Item 5) ایجاد می‌کند
    /// - برنامه پرداخت (Item 6) را بازمحاسبه می‌کند
    /// - لاگ حسابرسی ثبت می‌کند
    /// </summary>
    Task<ContractAmendment?> ApplyAmendmentAsync(
        int amendmentId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// رد اصلاحیه (از وضعیت PendingApproval به Rejected)
    /// </summary>
    Task<ContractAmendment?> RejectAmendmentAsync(
        int amendmentId, string currentUserId, string? comment = null, CancellationToken ct = default);

    /// <summary>
    /// لغو اصلاحیه (از هر وضعیتی به Cancelled)
    /// </summary>
    Task<ContractAmendment?> CancelAmendmentAsync(
        int amendmentId, string currentUserId, CancellationToken ct = default);
}