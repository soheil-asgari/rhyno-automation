using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Services;

public sealed class FinancialInvoiceService
{
    private readonly FinanceDbContext _context;
    private readonly IdentityDbContext _identityContext;
    private readonly NotificationService? _notificationService;
    private readonly ICurrentDataAccessScope? _dataAccessScope;

    public FinancialInvoiceService(
        FinanceDbContext context,
        IdentityDbContext identityContext,
        NotificationService? notificationService = null,
        ICurrentDataAccessScope? dataAccessScope = null)
    {
        _context = context;
        _identityContext = identityContext;
        _notificationService = notificationService;
        _dataAccessScope = dataAccessScope;
    }

    public async Task<FinancialInvoiceIndexVM> BuildInvoiceIndexAsync(
        FinancialInvoiceIndexVM filter,
        string invoiceType,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(invoiceType, "Purchase", StringComparison.OrdinalIgnoreCase))
        {
            await PublishPurchaseDeadlineNotificationsAsync(cancellationToken);
        }

        var query = _context.Invoices
            .AsNoTracking()
            .Include(item => item.Items)
            .Where(item => item.InvoiceType == invoiceType)
            .AsQueryable();
        if (_dataAccessScope != null)
        {
            query = query.ApplyCurrentAccessScope(_dataAccessScope);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(item =>
                item.InvoiceNumber.Contains(term) ||
                item.PartyName.Contains(term) ||
                item.VendorName.Contains(term) ||
                (item.NationalCodeOrEconomicId ?? string.Empty).Contains(term));
        }

        if (filter.Year.HasValue)
        {
            var yearText = filter.Year.Value.ToString(CultureInfo.InvariantCulture);
            query = query.Where(item => item.DateShamsi.StartsWith(yearText));
        }

        if (filter.Quarter is >= 1 and <= 4)
        {
            var quarterTokens = GetQuarterMonthTokens(filter.Quarter.Value);
            query = query.Where(item => quarterTokens.Any(token => item.DateShamsi.Contains(token)));
        }

        return new FinancialInvoiceIndexVM
        {
            SearchTerm = filter.SearchTerm,
            Year = filter.Year,
            Quarter = filter.Quarter,
            InvoiceType = invoiceType,
            Items = await query.OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken)
        };
    }

    public InvoiceCalculationResult CalculateTotals(IEnumerable<FinancialInvoiceItemVM> items)
    {
        var validItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemName) && item.Quantity > 0)
            .ToList();

        foreach (var item in validItems)
        {
            item.LineSubTotal = Math.Round(item.Quantity * item.UnitPrice, 2);
            item.LineVatAmount = Math.Round(item.LineSubTotal * 0.10m, 2);
            item.LineGrandTotal = item.LineSubTotal + item.LineVatAmount;
        }

        return new InvoiceCalculationResult(
            validItems,
            validItems.Sum(item => item.LineSubTotal),
            validItems.Sum(item => item.LineVatAmount),
            validItems.Sum(item => item.LineGrandTotal));
    }

    public async Task<bool> IsDuplicateInvoiceNumberAsync(
        string? invoiceNumber,
        string? invoiceType,
        int? currentInvoiceId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(invoiceNumber)
            ? string.Empty
            : PersianTextNormalizer.Normalize(invoiceNumber).Trim();

        if (string.IsNullOrWhiteSpace(normalizedNumber))
        {
            return false;
        }

        var normalizedType = string.IsNullOrWhiteSpace(invoiceType) ? "Sale" : invoiceType.Trim();
        return await _context.Invoices
            .AsNoTracking()
            .AnyAsync(item =>
                item.InvoiceNumber == normalizedNumber &&
                item.InvoiceType == normalizedType &&
                (!currentInvoiceId.HasValue || item.Id != currentInvoiceId.Value),
                cancellationToken);
    }

    private static List<string> GetQuarterMonthTokens(int quarter)
    {
        var months = quarter switch
        {
            1 => new[] { 1, 2, 3 },
            2 => new[] { 4, 5, 6 },
            3 => new[] { 7, 8, 9 },
            _ => new[] { 10, 11, 12 }
        };

        return months.Select(item => $"/{item:00}/").ToList();
    }

    private async Task PublishPurchaseDeadlineNotificationsAsync(CancellationToken cancellationToken)
    {
        if (_notificationService == null)
        {
            return;
        }

        var candidates = await _context.Invoices
            .AsNoTracking()
            .Where(item =>
                item.InvoiceType == "Purchase" &&
                item.DeadlineDateShamsi != null &&
                item.WorkflowStatus != WorkflowStatus.Approved &&
                item.WorkflowStatus != WorkflowStatus.Archived)
            .Select(item => new
            {
                item.Id,
                item.InvoiceNumber,
                item.PartyName,
                item.GrandTotal,
                item.DeadlineDateShamsi
            })
            .ToListAsync(cancellationToken);

        var today = DateTime.Today;
        var maxDate = today.AddDays(7);
        var dueInvoices = candidates
            .Select(item => new
            {
                Invoice = item,
                HasDate = TryParseShamsiDate(item.DeadlineDateShamsi, out var deadline),
                Deadline = deadline
            })
            .Where(item => item.HasDate && item.Deadline.Date >= today && item.Deadline.Date <= maxDate)
            .OrderBy(item => item.Deadline)
            .Take(12)
            .ToList();

        if (dueInvoices.Count == 0)
        {
            return;
        }

        var recipientIds = await GetFinanceNotificationRecipientsAsync(cancellationToken);
        if (recipientIds.Count == 0)
        {
            return;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddDays(8);
        foreach (var dueInvoice in dueInvoices)
        {
            var daysLeft = (dueInvoice.Deadline.Date - today).Days;
            var severity = daysLeft <= 1 ? NotificationSeverity.Danger : NotificationSeverity.Warning;
            var title = daysLeft == 0 ? "موعد پیگیری فاکتور خرید امروز است" : "موعد پیگیری فاکتور خرید نزدیک است";
            var message = $"{dueInvoice.Invoice.InvoiceNumber} - {dueInvoice.Invoice.PartyName} | موعد: {dueInvoice.Invoice.DeadlineDateShamsi} | مبلغ: {dueInvoice.Invoice.GrandTotal:N0}";

            foreach (var recipientId in recipientIds)
            {
                await _notificationService.UpsertActiveAsync(
                    recipientId,
                    title,
                    message,
                    severity,
                    "/Financial/EditInvoice/" + dueInvoice.Invoice.Id,
                    "Finance",
                    "InvoiceDeadline",
                    dueInvoice.Invoice.Id,
                    expiresAt,
                    cancellationToken);
            }
        }
    }

    public async Task PublishInvoiceDecisionNotificationAsync(
        Invoice invoice,
        string decision,
        string severity,
        CancellationToken cancellationToken = default)
    {
        if (_notificationService == null)
        {
            return;
        }

        var recipientIds = await GetFinanceNotificationRecipientsAsync(cancellationToken);
        if (recipientIds.Count == 0)
        {
            return;
        }

        var title = decision == WorkflowStatus.Approved
            ? "فاکتور مالی تایید شد"
            : "فاکتور مالی رد شد";
        var message = $"{invoice.InvoiceNumber} - {invoice.PartyName} | مبلغ: {invoice.GrandTotal:N0}";

        foreach (var recipientId in recipientIds)
        {
            await _notificationService.UpsertActiveAsync(
                recipientId,
                title,
                message,
                severity,
                "/Financial/EditInvoice/" + invoice.Id,
                "Finance",
                "InvoiceDecision",
                invoice.Id,
                DateTimeOffset.UtcNow.AddDays(14),
                cancellationToken);
        }
    }

    private async Task<List<string>> GetFinanceNotificationRecipientsAsync(CancellationToken cancellationToken)
    {
        var roleBasedUsers =
            from userRole in _identityContext.UserRoles.AsNoTracking()
            join rolePermission in _identityContext.RolePermissions.AsNoTracking()
                on userRole.RoleId equals rolePermission.RoleId
            where rolePermission.PermissionKey == "Finance.View" || rolePermission.PermissionKey == "Finance.Approve"
            select userRole.UserId;

        var flagBasedUsers = _identityContext.Users
            .AsNoTracking()
            .Where(user => user.CanAccessFinance)
            .Select(user => user.Id);

        return await roleBasedUsers
            .Union(flagBasedUsers)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static bool TryParseShamsiDate(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Replace('-', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var year) ||
            !int.TryParse(parts[1], out var month) ||
            !int.TryParse(parts[2], out var day))
        {
            return false;
        }

        try
        {
            result = new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}

public sealed record InvoiceCalculationResult(
    List<FinancialInvoiceItemVM> ValidItems,
    decimal SubTotal,
    decimal VatAmount,
    decimal GrandTotal);
