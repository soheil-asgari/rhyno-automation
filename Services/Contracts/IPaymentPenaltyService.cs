using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس جریمه دیرکرد پرداخت — محاسبه و اعمال خودکار جریمه
/// </summary>
public interface IPaymentPenaltyService
{
    /// <summary>
    /// ایجاد قانون پیش‌فرض برای یک قرارداد (2% ماهانه، بدون سقف، 0 روز تنفس)
    /// </summary>
    Task<PaymentPenaltyRule> CreateDefaultRuleAsync(int contractId, CancellationToken ct = default);

    /// <summary>
    /// دریافت قانون جریمه فعال برای یک قرارداد
    /// </summary>
    Task<PaymentPenaltyRule?> GetActiveRuleAsync(int contractId, CancellationToken ct = default);

    /// <summary>
    /// ذخیره یا به‌روزرسانی قانون جریمه
    /// </summary>
    Task<PaymentPenaltyRule> SaveRuleAsync(PaymentPenaltyRuleEditVM model, int? ruleId = null, CancellationToken ct = default);

    /// <summary>
    /// محاسبه جریمه برای یک پرداخت دیرکرد
    /// </summary>
    Task<CalculatedPenaltyVM?> CalculatePenaltyAsync(int paymentId, CancellationToken ct = default);

    /// <summary>
    /// اجرای دسته‌ای محاسبه و اعمال جریمه‌ها (برای BackgroundJob)
    /// </summary>
    Task<int> ApplyCalculatedPenaltiesAsync(CancellationToken ct = default);

    /// <summary>
    /// دریافت لیست جریمه‌های محاسبه شده برای یک قرارداد
    /// </summary>
    Task<List<CalculatedPenaltyVM>> GetPenaltiesForContractAsync(int contractId, CancellationToken ct = default);

    /// <summary>
    /// دریافت لیست قوانین جریمه برای یک قرارداد
    /// </summary>
    Task<List<PaymentPenaltyRuleVM>> GetRulesForContractAsync(int contractId, CancellationToken ct = default);

    /// <summary>
    /// تغییر وضعیت جریمه (بخشودن/اعمال)
    /// </summary>
    Task<bool> UpdatePenaltyStatusAsync(int penaltyId, string newStatus, CancellationToken ct = default);
}