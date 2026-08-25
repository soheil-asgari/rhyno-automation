using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class ContractTypeFieldVM
{
    public int Id { get; set; }
    public string ContractType { get; set; } = string.Empty;
    public string FieldKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text";
    public string? Options { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public string? SectionName { get; set; }
    public string? Placeholder { get; set; }
    public string? ValidationPattern { get; set; }
    public string? ValidationMessage { get; set; }
    public string? Value { get; set; }
}

public class BulkActionRequest
{
    public List<int> IDs { get; set; } = new();
    public string Action { get; set; } = string.Empty;
    public string? NewStatus { get; set; }
    public string? AssignToUserId { get; set; }
}

public class BulkActionResult
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ContractGuaranteeVM
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int? AmendmentId { get; set; }
    public string GuaranteeType { get; set; } = string.Empty;
    public string GuaranteeTypeDisplay => GuaranteeTypeNames.DisplayNames.TryGetValue(GuaranteeType, out var d) ? d : GuaranteeType;
    public string GuaranteeNumber { get; set; } = string.Empty;
    public string? IssuingBank { get; set; }
    public decimal Amount { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay => GuaranteeStatusNames.DisplayNames.TryGetValue(Status, out var d) ? d : Status;
    public string BadgeClass => GuaranteeStatusNames.BadgeColors.TryGetValue(Status, out var c) ? c : "";
    public decimal? RetentionPercentage { get; set; }
    public string? ReleaseCondition { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public bool IsExpiringSoon => (ExpiryDate.Date - DateTime.Today).Days <= 30 && Status == "Active";
    public bool CanRelease => Status == "Active" || Status == "Expiring";
    public string? CreatedByUserName { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ContractGuaranteeCreateVM
{
    [Required] public int ContractId { get; set; }
    [Required][StringLength(40)] public string GuaranteeType { get; set; } = GuaranteeTypeNames.BankGuarantee;
    [Required][StringLength(100)] public string GuaranteeNumber { get; set; } = string.Empty;
    [StringLength(200)] public string? IssuingBank { get; set; }
    [Range(0, 999999999999)] public decimal Amount { get; set; }
    [Required][DataType(DataType.Date)] public DateTime IssuedDate { get; set; } = DateTime.Today;
    [Required][DataType(DataType.Date)] public DateTime ExpiryDate { get; set; }
    public int? AmendmentId { get; set; }
    [Range(0, 100)] public decimal? RetentionPercentage { get; set; }
    [StringLength(500)] public string? ReleaseCondition { get; set; }
}

public class ContractGuaranteeEditVM
{
    public int Id { get; set; }
    [Required] public int ContractId { get; set; }
    [Required][StringLength(40)] public string GuaranteeType { get; set; } = GuaranteeTypeNames.BankGuarantee;
    [Required][StringLength(100)] public string GuaranteeNumber { get; set; } = string.Empty;
    [StringLength(200)] public string? IssuingBank { get; set; }
    [Range(0, 999999999999)] public decimal Amount { get; set; }
    [Required][DataType(DataType.Date)] public DateTime IssuedDate { get; set; }
    [Required][DataType(DataType.Date)] public DateTime ExpiryDate { get; set; }
    [Range(0, 100)] public decimal? RetentionPercentage { get; set; }
    [StringLength(500)] public string? ReleaseCondition { get; set; }
}
public class PaymentPenaltyRuleVM
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int GracePeriodDays { get; set; }
    public string PenaltyType { get; set; } = string.Empty;
    public string PenaltyTypeDisplay => PenaltyTypeNames.DisplayNames.TryGetValue(PenaltyType, out var d) ? d : PenaltyType;
    public decimal? PenaltyAmount { get; set; }
    public decimal? PenaltyPercentage { get; set; }
    public string CapType { get; set; } = string.Empty;
    public string CapTypeDisplay => CapTypeNames.DisplayNames.TryGetValue(CapType, out var d) ? d : CapType;
    public decimal? CapValue { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentPenaltyRuleEditVM
{
    [Required] public int ContractId { get; set; }
    [Range(0, 365)] public int GracePeriodDays { get; set; }
    [Required][StringLength(30)] public string PenaltyType { get; set; } = PenaltyTypeNames.PercentagePerMonth;
    [Range(0, 999999999999)] public decimal? PenaltyAmount { get; set; }
    [Range(0, 100)] public decimal? PenaltyPercentage { get; set; }
    [Required][StringLength(20)] public string CapType { get; set; } = CapTypeNames.None;
    [Range(0, 999999999999)] public decimal? CapValue { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CalculatedPenaltyVM
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public int ContractId { get; set; }
    public decimal CalculatedAmount { get; set; }
    public DateTime AppliedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay => PenaltyStatusNames.DisplayNames.TryGetValue(Status, out var d) ? d : Status;
    public string BadgeClass => PenaltyStatusNames.BadgeColors.TryGetValue(Status, out var c) ? c : "";
    public string? CalculationBasis { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PenaltySectionVM
{
    public List<PaymentPenaltyRuleVM> Rules { get; set; } = new();
    public List<CalculatedPenaltyVM> Penalties { get; set; } = new();
}

public class DocumentRequirementItemVM
{
    public int Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentCategory { get; set; } = string.Empty;
    public string DocumentCategoryDisplay => DocumentRequirementCategories.DisplayNames.TryGetValue(DocumentCategory, out var d) ? d : DocumentCategory;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
    public bool IsUploaded { get; set; }
    public int? UploadedAttachmentId { get; set; }
    public string? UploadedFileName { get; set; }
    public string Status => IsUploaded ? "Uploaded" : (IsRequired ? "Missing" : "Optional");
    public string StatusBadgeClass => DocumentStatus.BadgeClasses.TryGetValue(Status, out var c) ? c : "";
    public string StatusDisplay => Status switch { "Uploaded" => "آپلود شده", "Missing" => "آپلود نشده", "Optional" => "اختیاری", _ => Status };
}

public class DocumentRequirementChecklistVM
{
    public int ContractId { get; set; }
    public string? ContractType { get; set; }
    public List<DocumentRequirementItemVM> Items { get; set; } = new();
    public int RequiredCount => Items.Count(i => i.IsRequired);
    public int UploadedRequiredCount => Items.Count(i => i.IsRequired && i.IsUploaded);
    public int ProgressPercent => RequiredCount > 0 ? (int)Math.Min(100, Math.Round((double)UploadedRequiredCount / RequiredCount * 100)) : 100;
    public bool AllRequiredUploaded => RequiredCount == 0 || UploadedRequiredCount >= RequiredCount;
    public string? MissingRequiredNames => Items.Where(i => i.IsRequired && !i.IsUploaded).Select(i => i.DocumentName).ToList() is { Count: > 0 } m ? string.Join("، ", m) : null;
}

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
public class AgingBucketVM
{
    public string BucketName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public List<AgingContractItemVM> Contracts { get; set; } = new();
}

public class AgingContractItemVM
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int DaysOverdue { get; set; }
    public DateTime DueDate { get; set; }
}

public class PaymentAgingReportVM
{
    public List<AgingBucketVM> Buckets { get; set; } = new();
    public decimal TotalOverdue { get; set; }
    public int TotalOverdueCount { get; set; }
    public decimal AverageOverdueAmount { get; set; }
    public int MaxDaysOverdue { get; set; }
}

public class RevenuePeriodVM
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal RecognizedAmount { get; set; }
    public decimal ScheduledAmount { get; set; }
    public decimal TotalContractValueAtPeriodEnd { get; set; }
}

public class RevenueRecognitionReportVM
{
    public List<RevenuePeriodVM> Periods { get; set; } = new();
    public decimal TotalRecognized { get; set; }
    public decimal TotalScheduled { get; set; }
    public decimal TotalActiveValue { get; set; }
    public int ActiveContractCount { get; set; }
    public List<ChartDataPoint> CumulativeChart { get; set; } = new();
}

public class ValuePipelineItemVM
{
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay => ContractStatuses.All.Contains(Status) ? Status : Status;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageValue => Count > 0 ? TotalValue / Count : 0;
    public decimal PercentageOfTotal { get; set; }
}

public class ContractValuePipelineVM
{
    public List<ValuePipelineItemVM> Items { get; set; } = new();
    public decimal GrandTotalValue { get; set; }
    public int GrandTotalCount { get; set; }
    public decimal AverageContractValue { get; set; }
    public List<ChartDataPoint> Distribution { get; set; } = new();
}

public class ObligationEventVM
{
    public DateTime Date { get; set; }
    public string ObligationType { get; set; } = string.Empty;
    public string ObligationTypeDisplay => ObligationType switch
    {
        "PaymentDue" => "سررسید پرداخت", "RenewalApproaching" => "نزدیک به تمدید",
        "GuaranteeExpiring" => "انقضای ضمانت", _ => ObligationType
    };
    public string ObligationIcon => ObligationType switch
    {
        "PaymentDue" => "bi-credit-card", "RenewalApproaching" => "bi-arrow-repeat",
        "GuaranteeExpiring" => "bi-shield-exclamation", _ => "bi-calendar-event"
    };
    public string ObligationColor => ObligationType switch
    {
        "PaymentDue" => "text-rose-500", "RenewalApproaching" => "text-amber-500",
        "GuaranteeExpiring" => "text-sky-500", _ => "text-slate-500"
    };
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public List<ObligationContractItemVM> Contracts { get; set; } = new();
}

public class ObligationContractItemVM
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime EventDate { get; set; }
    public string? Description { get; set; }
}

public class ObligationsCalendarVM
{
    public List<ObligationEventVM> Events { get; set; } = new();
    public int TotalEvents { get; set; }
    public decimal TotalAmountDue { get; set; }
    public int UpcomingMonths { get; set; } = 6;
    public Dictionary<string, int> CountByType { get; set; } = new();
    public Dictionary<string, decimal> AmountByType { get; set; } = new();
}

public class CurrencyBreakdownVM
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ContractCount { get; set; }
    public decimal Percentage { get; set; }
}

public class FinancialExposureVM
{
    public decimal TotalActiveValue { get; set; }
    public int ActiveContractCount { get; set; }
    public decimal AtRiskValue { get; set; }
    public int AtRiskCount { get; set; }
    public decimal PendingApprovalValue { get; set; }
    public int PendingApprovalCount { get; set; }
    public decimal TotalOutstandingReceivables { get; set; }
    public decimal TotalRecognizedRevenue { get; set; }
    public decimal ExposureRatioPercent => TotalActiveValue > 0 ? Math.Round(AtRiskValue / TotalActiveValue * 100, 1) : 0;
    public List<CurrencyBreakdownVM> CurrencyBreakdown { get; set; } = new();
}

public class ReportsIndexVM
{
    public PaymentAgingReportVM? PaymentAging { get; set; }
    public RevenueRecognitionReportVM? RevenueRecognition { get; set; }
    public ContractValuePipelineVM? ValuePipeline { get; set; }
    public ObligationsCalendarVM? ObligationsCalendar { get; set; }
    public FinancialExposureVM? FinancialExposure { get; set; }
    public string ActiveTab { get; set; } = "financial-exposure";
    public int ObligationsMonths { get; set; } = 6;
}