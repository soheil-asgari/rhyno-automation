using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس مدیریت برنامه زمان‌بندی پرداخت قراردادها
/// </summary>
public interface IPaymentScheduleService
{
    /// <summary>
    /// تولید برنامه پرداخت برای یک قرارداد بر اساس نوع برنامه و تعداد اقساط.
    /// </summary>
    /// <param name="model">ViewModel حاوی تنظیمات برنامه</param>
    /// <param name="currentUserId">کاربر ایجادکننده</param>
    /// <param name="ct">توکن کنسل</param>
    /// <returns>برنامه ایجادشده به همراه اقساط</returns>
    Task<ContractPaymentSchedule> GenerateScheduleAsync(
        PaymentScheduleVM model,
        string currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// به‌روزرسانی دستی مبلغ اقساط در حالت Custom — پس از تولید اولیه
    /// </summary>
    Task<ContractPaymentSchedule> CustomizeInstallmentsAsync(
        int scheduleId, List<PaymentScheduleInstallmentVM> installments,
        string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// دریافت برنامه پرداخت یک قرارداد (به همراه اقساط)
    /// </summary>
    Task<PaymentScheduleVM?> GetScheduleAsync(
        int contractId,
        CancellationToken ct = default);

    /// <summary>
    /// حذف برنامه پرداخت یک قرارداد
    /// </summary>
    Task<bool> DeleteScheduleAsync(
        int scheduleId,
        string currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// به‌روزرسانی وضعیت اقساط بر اساس تاریخ سررسید (Overdue)
    /// </summary>
    Task UpdateOverdueStatusAsync(CancellationToken ct = default);
}