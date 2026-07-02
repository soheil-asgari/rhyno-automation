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
}
