using System.Threading.Tasks;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس گزارش‌های پیشرفته قراردادها — Aging, Revenue Pipeline, Obligations Calendar, Financial Exposure
/// </summary>
public interface IContractReportService
{
    /// <summary>گزارش پیری پرداخت (حجم معوقات در بازه‌های 0-30, 31-60, 61-90, 90+ روز)</summary>
    Task<PaymentAgingReportVM> GetPaymentAgingReportAsync(CancellationToken ct = default);

    /// <summary>گزارش شناسایی درآمد ماهانه (Accrual) — تعهدی</summary>
    Task<RevenueRecognitionReportVM> GetRevenueRecognitionScheduleAsync(CancellationToken ct = default);

    /// <summary>خط لوله ارزش قراردادها بر اساس وضعیت</summary>
    Task<ContractValuePipelineVM> GetContractValuePipelineAsync(CancellationToken ct = default);

    /// <summary>تقویم تعهدات — پرداخت‌های آتی، تمدیدها، انقضای ضمانت‌ها</summary>
    Task<ObligationsCalendarVM> GetObligationsCalendarAsync(int months = 6, CancellationToken ct = default);

    /// <summary>خلاصه ریسک مالی — ارزش در معرض خطر، مواجهه ارزی</summary>
    Task<FinancialExposureVM> GetFinancialExposureAsync(CancellationToken ct = default);

    /// <summary>گزارش جامع (همه گزارش‌ها در یک صفحه)</summary>
    Task<ReportsIndexVM> GetAllReportsAsync(int obligationsMonths = 6, CancellationToken ct = default);
}