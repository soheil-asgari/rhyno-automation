using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس حسابداری قراردادها — ثبت سند حسابداری (Voucher) به ازای هر رویداد مالی
/// قرارداد با رعایت مبنای تعهدی (Accrual Basis).
///
/// جریان (Flow):
/// 1. هنگام فعال شدن قرارداد ← بدهکار: دریافتنی / بستانکار: درآمد معوق (Deferred Revenue)
/// 2. هر پرداخت ← بدهکار: وجه نقد / بستانکار: دریافتنی (کاهش مانده دریافتنی)
/// 3. ماهانه (توسط BackgroundJob) ← بدهکار: درآمد معوق / بستانکار: درآمد شناسایی‌شده
///    به نسبت مدت سپری شده از قرارداد.
/// </summary>
public interface IContractAccountingService
{
    /// <summary>
    /// مرحله 1: ثبت سند افتتاحیه — دریافتنی (Debit) / درآمد معوق (Credit). یک بار هنگام فعال شدن قرارداد.
    /// </summary>
    Task RecordDeferredRevenueAsync(Contract contract, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// مرحله 2: ثبت سند دریافت وجه — بدهکار: بانک/صندوق / بستانکار: دریافتنی.
    /// </summary>
    Task RecordCashReceiptAsync(Contract contract, ContractPayment payment, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// برگشت سند دریافت وجه هنگام حذف پرداخت.
    /// </summary>
    Task RevertCashReceiptAsync(Contract contract, ContractPayment deletedPayment, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// مرحله 3: شناسایی بخشی از درآمد معوق به عنوان درآمد قطعی به نسبت روزهای سپری شده.
    /// </summary>
    Task<decimal> RecognizeRevenueAsync(int contractId, DateTime asOfDate, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// اجرای دسته‌ای برای همه قراردادهای فعال.
    /// </summary>
    Task<int> RecognizeAllActiveContractsAsync(DateTime asOfDate, string systemUserId, CancellationToken ct = default);
}