using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس جایگزاری فیلدهای پویا در قالب‌های HTML قرارداد
/// پشتیبانی از placeholders: {{CustomerName}}, {{TotalAmount}}, {{StartDate}}, {{SignDate}},
/// {{ContractNumber}}, {{AssignedUser}}, {{CustomerAddress}}, {{CustomerPhone}}, {{EndDate}},
/// {{PaidAmount}}, {{RemainingBalance}}, {{PaymentStatus}}, {{ContractType}}, {{Description}}, {{CompanyName}}
/// </summary>
public interface ITemplateRenderingService
{
    /// <summary>
    /// جایگزاری placeholders در قالب HTML با داده‌های واقعی قرارداد
    /// </summary>
    string RenderTemplate(string htmlTemplate, Contract contract, Customer? customer = null, User? assignedUser = null);

    /// <summary>
    /// تولید پیش‌نمایش HTML از قالب با داده‌های نمونه
    /// </summary>
    string RenderPreview(string htmlTemplate);

    /// <summary>
    /// دریافت لیست تگ‌های قابل استفاده
    /// </summary>
    List<TemplateTagInfo> GetAvailableTags();
}

public class TemplateTagInfo
{
    public string Tag { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class TemplateRenderingService : ITemplateRenderingService
{
    private const string DefaultCompanyName = "شرکت راینو";

    public string RenderTemplate(string htmlTemplate, Contract contract, Customer? customer = null, User? assignedUser = null)
    {
        if (string.IsNullOrWhiteSpace(htmlTemplate))
            return string.Empty;

        // Helper to encode plain-text field values inserted into an HTML document,
        // preventing stored XSS via contract text fields (Title, ClientName, Terms, ...).
        static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "");

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{ContractNumber}}"] = Encode(contract.ContractNumber),
            ["{{Title}}"] = Encode(contract.Title),
            ["{{ContractType}}"] = Encode(contract.ContractType ?? "-"),
            ["{{Description}}"] = Encode(contract.Description),
            ["{{CustomerName}}"] = Encode(customer?.Name ?? contract.ClientName),
            ["{{CustomerCompany}}"] = Encode(customer?.CompanyName ?? contract.ClientName),
            ["{{CustomerPhone}}"] = Encode(customer?.Phone ?? contract.ClientPhone ?? "-"),
            ["{{CustomerAddress}}"] = Encode(customer?.Address ?? contract.ClientAddress ?? "-"),
            ["{{SecondPartyPhone}}"] = Encode(contract.ClientPhone ?? "-"),
            ["{{AssignedUser}}"] = Encode(assignedUser?.FullName ?? contract.AssignedUser?.FullName ?? "-"),
            ["{{ResponsibleUser}}"] = Encode(assignedUser?.FullName ?? contract.AssignedUser?.FullName ?? "-"),
            ["{{StartDate}}"] = Encode(contract.StartDate.ToString("yyyy/MM/dd")),
            ["{{EndDate}}"] = Encode(contract.EndDate?.ToString("yyyy/MM/dd") ?? "-"),
            ["{{SignDate}}"] = Encode(contract.SignDate?.ToString("yyyy/MM/dd") ?? "-"),
            ["{{TotalAmount}}"] = Encode(contract.TotalAmount.ToString("N0")),
            ["{{PaidAmount}}"] = Encode(contract.PaidAmount.ToString("N0")),
            ["{{RemainingBalance}}"] = Encode(contract.RemainingBalance.ToString("N0")),
            ["{{PaymentStatus}}"] = Encode(contract.PaymentStatus switch
            {
                "Unpaid" => "پرداخت نشده",
                "PartiallyPaid" => "پرداخت جزئی",
                "FullyPaid" => "تسویه شده",
                _ => contract.PaymentStatus
            }),
            ["{{Status}}"] = Encode(contract.Status),
            ["{{Terms}}"] = Encode(contract.TermsAndConditions),
            ["{{CompanyName}}"] = Encode(DefaultCompanyName),
            ["{{ContractAmount}}"] = Encode(contract.TotalAmount.ToString("N0")),
            ["{{contract_number}}"] = Encode(contract.ContractNumber),
            ["{{contract_title}}"] = Encode(contract.Title),
            ["{{contract_type}}"] = Encode(contract.ContractType ?? "-"),
            ["{{customer_name}}"] = Encode(customer?.Name ?? contract.ClientName),
            ["{{customer_company}}"] = Encode(customer?.CompanyName ?? contract.ClientName),
            ["{{customer_phone}}"] = Encode(customer?.Phone ?? contract.ClientPhone ?? "-"),
            ["{{customer_address}}"] = Encode(customer?.Address ?? contract.ClientAddress ?? "-"),
            ["{{responsible_user}}"] = Encode(assignedUser?.FullName ?? contract.AssignedUser?.FullName ?? "-"),
            ["{{start_date}}"] = Encode(contract.StartDate.ToString("yyyy/MM/dd")),
            ["{{end_date}}"] = Encode(contract.EndDate?.ToString("yyyy/MM/dd") ?? "-"),
            ["{{signing_date}}"] = Encode(contract.SignDate?.ToString("yyyy/MM/dd") ?? "-"),
            ["{{contract_amount}}"] = Encode(contract.TotalAmount.ToString("N0")),
            ["{{paid_amount}}"] = Encode(contract.PaidAmount.ToString("N0")),
            ["{{remaining_balance}}"] = Encode(contract.RemainingBalance.ToString("N0")),
            ["{{company_name}}"] = Encode(DefaultCompanyName),
            ["{{description}}"] = Encode(contract.Description),
            ["{{terms}}"] = Encode(contract.TermsAndConditions),
            ["{{status}}"] = Encode(contract.Status)
        };

        foreach (var kvp in replacements)
        {
            htmlTemplate = htmlTemplate.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
        }

        return htmlTemplate;
    }

    public string RenderPreview(string htmlTemplate)
    {
        if (string.IsNullOrWhiteSpace(htmlTemplate))
            return string.Empty;

        var sampleReplacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{ContractNumber}}"] = "CTR-1403-001",
            ["{{Title}}"] = "نمونه قرارداد",
            ["{{ContractType}}"] = "خدمات",
            ["{{CustomerName}}"] = "شرکت نمونه",
            ["{{CustomerCompany}}"] = "شرکت نمونه",
            ["{{CustomerPhone}}"] = "۰۲۱-۱۲۳۴۵۶۷۸",
            ["{{CustomerAddress}}"] = "تهران، خیابان نمونه",
            ["{{SecondPartyPhone}}"] = "۰۲۱-۱۲۳۴۵۶۷۸",
            ["{{AssignedUser}}"] = "کاربر نمونه",
            ["{{ResponsibleUser}}"] = "کاربر نمونه",
            ["{{StartDate}}"] = "1403/01/01",
            ["{{EndDate}}"] = "1403/12/29",
            ["{{SignDate}}"] = "1403/01/15",
            ["{{TotalAmount}}"] = "۱۰۰,۰۰۰,۰۰۰",
            ["{{PaidAmount}}"] = "۵۰,۰۰۰,۰۰۰",
            ["{{RemainingBalance}}"] = "۵۰,۰۰۰,۰۰۰",
            ["{{PaymentStatus}}"] = "پرداخت جزئی",
            ["{{Status}}"] = "فعال",
            ["{{CompanyName}}"] = DefaultCompanyName,
            ["{{Terms}}"] = "شرایط و ضوابط نمونه...",
            ["{{Description}}"] = "توضیحات نمونه..."
        };

        // Also handle lowercase versions
        foreach (var kvp in sampleReplacements.ToList())
        {
            var lowerKey = kvp.Key.ToLowerInvariant();
            if (!sampleReplacements.ContainsKey(lowerKey))
            {
                sampleReplacements[lowerKey] = kvp.Value;
            }
        }

        foreach (var kvp in sampleReplacements)
        {
            htmlTemplate = htmlTemplate.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
        }

        return htmlTemplate;
    }

    public List<TemplateTagInfo> GetAvailableTags()
    {
        return
        [
            new() { Tag = "{{CustomerName}}", DisplayName = "نام مشتری", Description = "نام مشتری یا طرف قرارداد" },
            new() { Tag = "{{CustomerCompany}}", DisplayName = "شرکت مشتری", Description = "نام شرکت مشتری" },
            new() { Tag = "{{CustomerPhone}}", DisplayName = "تلفن مشتری", Description = "شماره تلفن مشتری" },
            new() { Tag = "{{CustomerAddress}}", DisplayName = "آدرس مشتری", Description = "آدرس مشتری" },
            new() { Tag = "{{SecondPartyPhone}}", DisplayName = "تلفن طرف قرارداد", Description = "شماره تلفن طرف دوم" },
            new() { Tag = "{{TotalAmount}}", DisplayName = "مبلغ کل", Description = "مبلغ کل قرارداد" },
            new() { Tag = "{{PaidAmount}}", DisplayName = "مبلغ پرداخت شده", Description = "مبلغ پرداخت شده" },
            new() { Tag = "{{RemainingBalance}}", DisplayName = "باقی مانده", Description = "مانده حساب" },
            new() { Tag = "{{StartDate}}", DisplayName = "تاریخ شروع", Description = "تاریخ شروع قرارداد" },
            new() { Tag = "{{EndDate}}", DisplayName = "تاریخ پایان", Description = "تاریخ پایان قرارداد" },
            new() { Tag = "{{SignDate}}", DisplayName = "تاریخ امضا", Description = "تاریخ امضای قرارداد" },
            new() { Tag = "{{ContractNumber}}", DisplayName = "شماره قرارداد", Description = "شماره یکتای قرارداد" },
            new() { Tag = "{{AssignedUser}}", DisplayName = "کاربر مسئول", Description = "نام کاربر مسئول پیگیری" },
            new() { Tag = "{{ContractType}}", DisplayName = "نوع قرارداد", Description = "نوع قرارداد" },
            new() { Tag = "{{PaymentStatus}}", DisplayName = "وضعیت پرداخت", Description = "وضعیت پرداخت (تسویه/جزئی/نشده)" },
            new() { Tag = "{{CompanyName}}", DisplayName = "نام شرکت", Description = "نام شرکت (راینو)" },
            new() { Tag = "{{Description}}", DisplayName = "توضیحات", Description = "توضیحات قرارداد" },
            new() { Tag = "{{Terms}}", DisplayName = "شرایط و ضوابط", Description = "شرایط و ضوابط قرارداد" },
            new() { Tag = "{{Status}}", DisplayName = "وضعیت قرارداد", Description = "وضعیت فعلی قرارداد" }
        ];
    }
}