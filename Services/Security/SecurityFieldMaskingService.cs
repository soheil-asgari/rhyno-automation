using System.Text.RegularExpressions;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Security;

public interface ISecurityFieldMaskingService
{
    Task MaskInvoicesAsync(FinancialInvoiceIndexVM model, CancellationToken cancellationToken = default);
    Task MaskHumanCapitalIndexAsync(HumanCapitalIndexVM model, CancellationToken cancellationToken = default);
    Task MaskHumanCapitalDetailsAsync(HumanCapitalDetailsVM model, CancellationToken cancellationToken = default);
    string MaskNationalId(string? value);
    string MaskPhone(string? value);
    string MaskEmail(string? value);
    string MaskAddress(string? value);
    string MaskSensitiveAuditText(string? value);
}

public sealed class SecurityFieldMaskingService : ISecurityFieldMaskingService
{
    private readonly ICurrentUserContextAccessor _currentUserContextAccessor;

    public SecurityFieldMaskingService(ICurrentUserContextAccessor currentUserContextAccessor)
    {
        _currentUserContextAccessor = currentUserContextAccessor;
    }

    public async Task MaskInvoicesAsync(FinancialInvoiceIndexVM model, CancellationToken cancellationToken = default)
    {
        var profile = _currentUserContextAccessor.CurrentProfile ?? await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
        if (profile == null || profile.Permissions.Contains("Finance.ViewSensitive") || profile.Permissions.Contains("Security.Manage"))
        {
            return;
        }

        foreach (var item in model.Items)
        {
            item.NationalCodeOrEconomicId = MaskNationalId(item.NationalCodeOrEconomicId);
            item.SubTotal = 0;
            item.VatAmount = 0;
            item.GrandTotal = 0;
            item.Amount = 0;
            if (item.Items != null)
            {
                foreach (var line in item.Items)
                {
                    line.UnitPrice = 0;
                    line.LineSubTotal = 0;
                    line.LineVatAmount = 0;
                    line.LineGrandTotal = 0;
                }
            }
        }
    }

    public async Task MaskHumanCapitalIndexAsync(HumanCapitalIndexVM model, CancellationToken cancellationToken = default)
    {
        var profile = _currentUserContextAccessor.CurrentProfile ?? await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
        if (profile == null || profile.Permissions.Contains("HR.ViewSensitive") || profile.Permissions.Contains("Security.Manage"))
        {
            return;
        }

        foreach (var item in model.Items)
        {
            item.NationalCode = MaskNationalId(item.NationalCode);
            item.PhoneNumber = MaskPhone(item.PhoneNumber);
            item.CurrentSalary = 0;
        }
    }

    public async Task MaskHumanCapitalDetailsAsync(HumanCapitalDetailsVM model, CancellationToken cancellationToken = default)
    {
        var profile = _currentUserContextAccessor.CurrentProfile ?? await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
        if (profile == null || profile.Permissions.Contains("HR.ViewSensitive") || profile.Permissions.Contains("Security.Manage"))
        {
            return;
        }

        model.NationalCode = MaskNationalId(model.NationalCode);
        model.PhoneNumber = MaskPhone(model.PhoneNumber);
        model.Email = MaskEmail(model.Email);
        model.Address = string.IsNullOrWhiteSpace(model.Address) ? model.Address : "******";
        model.CurrentSalary = 0;
        foreach (var item in model.SalaryHistories)
        {
            item.PreviousSalary = 0;
            item.NewSalary = 0;
        }
    }

    public string MaskNationalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var digits = Regex.Replace(value, "\\s+", string.Empty);
        return digits.Length <= 4 ? "****" : new string('*', Math.Max(0, digits.Length - 4)) + digits[^4..];
    }

    public string MaskPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= 4 ? "****" : normalized[..Math.Min(3, normalized.Length)] + "****" + normalized[^2..];
    }

    public string MaskEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var parts = value.Split('@');
        if (parts.Length != 2 || parts[0].Length <= 2)
        {
            return "***";
        }

        return parts[0][..2] + "***@" + parts[1];
    }

    public string MaskAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        return "[آدرس ماسک شد]";
    }

    public string MaskSensitiveAuditText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var result = value;

        // 1. Normalize Persian/Arabic digits to ASCII digits for robust pattern matching
        var normalized = NormalizePersianDigits(result);

        // 2. Detect and mask Iranian and international phone/mobile numbers:
        // Matches e.g. +989123456789, 00989123456789, 09123456789, 9123456789, landlines 02112345678, formatted 0912-345-6789 or 0912 345 6789
        result = Regex.Replace(result, @"(?:\+98|0098|0)?9\d{2}[\s\-]?\d{3}[\s\-]?\d{4}|\b(?:0[1-8]\d{1,2}[\s\-]?\d{7,8})\b", match =>
        {
            var cleanDigits = Regex.Replace(match.Value, @"[\s\-]", "");
            if (cleanDigits.Length < 7) return match.Value;
            // Keep first 2-3 digits and last 2 digits, mask the middle with ****
            var prefixLen = cleanDigits.StartsWith("+") ? 4 : (cleanDigits.StartsWith("00") ? 5 : (cleanDigits.Length >= 10 ? 3 : 2));
            if (cleanDigits.Length <= prefixLen + 2)
            {
                return cleanDigits[..2] + "****" + cleanDigits[^2..];
            }
            return cleanDigits[..prefixLen] + "****" + cleanDigits[^2..];
        });

        // Also run a regex pass on any remaining 10-11 digit sequences that look like phone numbers
        result = Regex.Replace(result, @"\b(0\d{9,10})\b", match =>
        {
            var val = match.Value;
            return val[..3] + "****" + val[^2..];
        });

        // 3. Mask Email addresses in description
        result = Regex.Replace(result, @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", match =>
        {
            return MaskEmail(match.Value);
        });

        // 4. Detect and mask address free-text patterns in Persian descriptions:
        // Matches phrases starting with آدرس: / نشانی: / محل سکونت: / اقامتگاه: (with optional separator like : or space)
        // followed by text up to sentence-ending punctuation or next section delimiter (e.g. . / ؛ / ; or newline or before another keyword like و ایمیل / ایمیل / تلفن)
        result = Regex.Replace(result, @"(?<=(?:آدرس|نشاني|نشانی|محل سکونت|اقامتگاه)\s*[:：\-]?\s*)([^\r\n.;؛]+?)(?=\s+(?:و\s+)?(?:ایمیل|پست الکترونیک|تلفن|موبایل|همراه)|[.;؛\r\n]|$)", "[آدرس ماسک شد]");

        // Also match street/alley address structures in text:
        // e.g. "خیابان ... پلاک ..." or "خ ... کوچه ... پلاک ..." (including internal Persian/Latin commas)
        result = Regex.Replace(result, @"(?:خیابان|بلوار|میدان|کوچه|کوچهٔ)\s+[^.;؛\r\n]+?(?:پلاک|واحد)\s+\d+[^.;؛\r\n]*", "[آدرس ماسک شد]");

        return result;
    }

    private static string NormalizePersianDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] >= '۰' && chars[i] <= '۹')
            {
                chars[i] = (char)('0' + (chars[i] - '۰'));
            }
            else if (chars[i] >= '٠' && chars[i] <= '٩')
            {
                chars[i] = (char)('0' + (chars[i] - '٠'));
            }
        }
        return new string(chars);
    }
}

