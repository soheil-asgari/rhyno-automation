using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Application.SavedViews;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Services;

public sealed class FinancialAppService
{
    private readonly FinanceDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IOfficeService _officeService;
    private readonly FinancialInvoiceService _invoiceService;
    private readonly FinanceLedgerService _ledgerService;
    private readonly PeriodClosingService _periodClosingService;
    private readonly TrialBalanceService _trialBalanceService;
    private readonly VoucherRenumberingService _voucherRenumberingService;
    private readonly WorkflowService _workflowService;
    private readonly ISegregationOfDutiesService _segregationOfDutiesService;
    private readonly IAbacAuthorizationService _abacAuthorizationService;
    private readonly ICurrentUserContextAccessor _currentUserContextAccessor;
    private readonly ISecurityFieldMaskingService _securityFieldMaskingService;
    private readonly ITableSchemaRegistry _tableSchemaRegistry;

    public FinancialAppService(
        FinanceDbContext context,
        IInventoryService inventoryService,
        IOfficeService officeService,
        FinancialInvoiceService invoiceService,
        FinanceLedgerService ledgerService,
        PeriodClosingService periodClosingService,
        TrialBalanceService trialBalanceService,
        VoucherRenumberingService voucherRenumberingService,
        WorkflowService workflowService,
        ISegregationOfDutiesService segregationOfDutiesService,
        IAbacAuthorizationService abacAuthorizationService,
        ICurrentUserContextAccessor currentUserContextAccessor,
        ISecurityFieldMaskingService securityFieldMaskingService,
        ITableSchemaRegistry tableSchemaRegistry)
    {
        _context = context;
        _inventoryService = inventoryService;
        _officeService = officeService;
        _invoiceService = invoiceService;
        _ledgerService = ledgerService;
        _periodClosingService = periodClosingService;
        _trialBalanceService = trialBalanceService;
        _voucherRenumberingService = voucherRenumberingService;
        _workflowService = workflowService;
        _segregationOfDutiesService = segregationOfDutiesService;
        _abacAuthorizationService = abacAuthorizationService;
        _currentUserContextAccessor = currentUserContextAccessor;
        _securityFieldMaskingService = securityFieldMaskingService;
        _tableSchemaRegistry = tableSchemaRegistry;
    }

    // ── Invoice CRUD ──────────────────────────────────────

    public async Task<FinancialInvoiceIndexVM> BuildInvoiceIndexAsync(FinancialInvoiceIndexVM filter, string invoiceType, CancellationToken cancellationToken)
    {
        var model = await _invoiceService.BuildInvoiceIndexAsync(filter, invoiceType, cancellationToken);
        await _securityFieldMaskingService.MaskInvoicesAsync(model, cancellationToken);
        return model;
    }

    public async Task<FinancialInvoiceUpsertVM> PrepareCreateInvoiceModelAsync(string? invoiceType, CancellationToken cancellationToken)
    {
        var normalizedType = invoiceType == "Purchase" ? "Purchase" : "Sale";
        var model = new FinancialInvoiceUpsertVM
        {
            DateShamsi = GetTodayShamsi(),
            InvoiceType = normalizedType,
            InvoiceNumber = await BuildNextInvoiceNumberAsync(cancellationToken),
            Items = new List<FinancialInvoiceItemVM> { new() }
        };

        await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
        await PopulatePurchaseOptionsAsync(model, cancellationToken);
        await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
        return model;
    }

    public async Task<(bool IsDuplicate, string Message)> CheckInvoiceNumberAsync(string? invoiceNumber, string? invoiceType, int? currentInvoiceId, CancellationToken cancellationToken)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? string.Empty : PersianTextNormalizer.Normalize(invoiceNumber);
        if (string.IsNullOrWhiteSpace(normalizedNumber))
        {
            return (false, string.Empty);
        }

        var duplicate = await _invoiceService.IsDuplicateInvoiceNumberAsync(normalizedNumber, invoiceType, currentInvoiceId, cancellationToken);
        return (duplicate, duplicate ? "این شماره فاکتور برای نوع انتخابی تکراری است." : "شماره فاکتور قابل استفاده است.");
    }

    public async Task<Invoice> CreateInvoiceAsync(FinancialInvoiceUpsertVM model, string currentUserId, CancellationToken cancellationToken)
    {
        var calculation = _invoiceService.CalculateTotals(model.Items);
        var validItems = calculation.ValidItems;
        model.SubTotal = calculation.SubTotal;
        model.VatAmount = calculation.VatAmount;
        model.GrandTotal = calculation.GrandTotal;

        var invoiceDate = TryParseShamsiDate(model.DateShamsi, out var parsedDate) ? parsedDate : DateTime.Now;
        var resolvedPartyName = model.InvoiceType == "Sale"
            ? await ResolveEmployerNameAsync(model.EmployerId, model.PartyName, cancellationToken)
            : (model.PartyName?.Trim() ?? string.Empty);

        var entity = new Invoice
        {
            InvoiceNumber = model.InvoiceNumber.Trim(),
            InvoiceType = model.InvoiceType,
            DateShamsi = PersianTextNormalizer.Normalize(model.DateShamsi),
            PartyName = resolvedPartyName,
            NationalCodeOrEconomicId = model.NationalCodeOrEconomicId?.Trim(),
            SubTotal = model.SubTotal,
            VatAmount = model.VatAmount,
            GrandTotal = model.GrandTotal,
            Notes = model.Notes?.Trim(),
            WorkflowStatus = WorkflowStatus.Sent,
            EmployerId = model.InvoiceType == "Sale" ? model.EmployerId : null,
            WarehouseReceiptId = model.InvoiceType == "Purchase" ? model.WarehouseReceiptId : null,
            FollowUpEmployeeId = model.InvoiceType == "Purchase" ? model.FollowUpEmployeeId : null,
            DeadlineDateShamsi = model.InvoiceType == "Purchase" ? NormalizeOptionalShamsi(model.DeadlineDateShamsi) : null,
            Amount = model.GrandTotal,
            VendorName = resolvedPartyName,
            InvoiceDate = invoiceDate,
            CreatedAt = DateTime.Now,
            CreatedByUserId = currentUserId,
            Items = validItems.Select(item => new InvoiceItem
            {
                ProductId = item.ProductId,
                ItemName = item.ItemName.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineSubTotal = item.LineSubTotal,
                LineVatAmount = item.LineVatAmount,
                LineGrandTotal = item.LineGrandTotal
            }).ToList()
        };

        _context.Invoices.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _workflowService.StartRoutingAsync("Invoice", currentUserId, currentUserId, entity.Id);

        return entity;
    }

    public async Task<FinancialInvoiceUpsertVM?> PrepareEditInvoiceModelAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _context.Invoices
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (entity == null)
        {
            return null;
        }

        var model = new FinancialInvoiceUpsertVM
        {
            Id = entity.Id,
            InvoiceNumber = entity.InvoiceNumber,
            InvoiceType = entity.InvoiceType,
            DateShamsi = entity.DateShamsi,
            PartyName = entity.PartyName,
            NationalCodeOrEconomicId = entity.NationalCodeOrEconomicId,
            SubTotal = entity.SubTotal,
            VatAmount = entity.VatAmount,
            GrandTotal = entity.GrandTotal,
            Notes = entity.Notes,
            WarehouseReceiptId = entity.WarehouseReceiptId,
            FollowUpEmployeeId = entity.FollowUpEmployeeId,
            EmployerId = entity.EmployerId,
            DeadlineDateShamsi = entity.DeadlineDateShamsi,
            Items = entity.Items
                .OrderBy(item => item.Id)
                .Select(item => new FinancialInvoiceItemVM
                {
                    ProductId = item.ProductId,
                    ItemName = item.ItemName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineSubTotal = item.LineSubTotal,
                    LineVatAmount = item.LineVatAmount,
                    LineGrandTotal = item.LineGrandTotal
                })
                .ToList()
        };

        await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
        await PopulatePurchaseOptionsAsync(model, cancellationToken);
        await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
        return model;
    }

    public async Task UpdateInvoiceAsync(int id, FinancialInvoiceUpsertVM model, CancellationToken cancellationToken)
    {
        var entity = await _context.Invoices
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (entity == null)
        {
            return;
        }

        var calculation = _invoiceService.CalculateTotals(model.Items);
        var validItems = calculation.ValidItems;
        model.SubTotal = calculation.SubTotal;
        model.VatAmount = calculation.VatAmount;
        model.GrandTotal = calculation.GrandTotal;

        var invoiceDate = TryParseShamsiDate(model.DateShamsi, out var parsedDate) ? parsedDate : entity.InvoiceDate;
        var resolvedPartyName = model.InvoiceType == "Sale"
            ? await ResolveEmployerNameAsync(model.EmployerId, model.PartyName, cancellationToken)
            : (model.PartyName?.Trim() ?? string.Empty);

        entity.InvoiceNumber = model.InvoiceNumber.Trim();
        entity.InvoiceType = model.InvoiceType;
        entity.DateShamsi = PersianTextNormalizer.Normalize(model.DateShamsi);
        entity.PartyName = resolvedPartyName;
        entity.NationalCodeOrEconomicId = model.NationalCodeOrEconomicId?.Trim();
        entity.SubTotal = model.SubTotal;
        entity.VatAmount = model.VatAmount;
        entity.GrandTotal = model.GrandTotal;
        entity.Notes = model.Notes?.Trim();
        entity.WorkflowStatus = WorkflowStatus.Normalize(entity.WorkflowStatus);
        entity.EmployerId = model.InvoiceType == "Sale" ? model.EmployerId : null;
        entity.WarehouseReceiptId = model.InvoiceType == "Purchase" ? model.WarehouseReceiptId : null;
        entity.FollowUpEmployeeId = model.InvoiceType == "Purchase" ? model.FollowUpEmployeeId : null;
        entity.DeadlineDateShamsi = model.InvoiceType == "Purchase" ? NormalizeOptionalShamsi(model.DeadlineDateShamsi) : null;
        entity.Amount = model.GrandTotal;
        entity.VendorName = resolvedPartyName;
        entity.InvoiceDate = invoiceDate;

        if (entity.Items.Count > 0)
        {
            _context.InvoiceItems.RemoveRange(entity.Items);
        }

        entity.Items = validItems.Select(item => new InvoiceItem
        {
            ProductId = item.ProductId,
            ItemName = item.ItemName.Trim(),
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            LineSubTotal = item.LineSubTotal,
            LineVatAmount = item.LineVatAmount,
            LineGrandTotal = item.LineGrandTotal
        }).ToList();

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(bool Success, string? InvoiceType)> DeleteInvoiceAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _context.Invoices
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (entity == null)
        {
            return (false, null);
        }

        var invoiceType = entity.InvoiceType;
        _context.Invoices.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return (true, invoiceType);
    }

    // ── Invoice Approval ──────────────────────────────────

    public async Task<(bool Success, string? ErrorMessage)> ApproveInvoiceAsync(int id, string currentUserId, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice == null)
        {
            return (false, null);
        }

        var sodResult = await _segregationOfDutiesService.ValidateFinanceApprovalAsync(invoice, currentUserId, cancellationToken);
        if (!sodResult.Allowed)
        {
            return (false, sodResult.Reason ?? "سیاست تفکیک وظایف اجازه تایید این فاکتور را نمی‌دهد.");
        }

        var profile = await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
        var abacResult = await _abacAuthorizationService.AuthorizeAsync(profile, new AbacResourceContext("Finance", "Approve", invoice.CreatedByUserId, null, true), cancellationToken);
        if (!abacResult.Allowed)
        {
            return (false, "Forbidden");
        }

        invoice.WorkflowStatus = WorkflowStatus.Approved;
        await _context.SaveChangesAsync(cancellationToken);
        await _ledgerService.PostInvoiceAsync(invoice, cancellationToken);
        await _workflowService.RecordDecisionAsync("Invoice", invoice.Id, 1, currentUserId, WorkflowStatus.Approved, cancellationToken: cancellationToken);
        await _invoiceService.PublishInvoiceDecisionNotificationAsync(invoice, WorkflowStatus.Approved, NotificationSeverity.Success, cancellationToken);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> RejectInvoiceAsync(int id, string currentUserId, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice == null)
        {
            return (false, null);
        }

        var profile = await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
        var abacResult = await _abacAuthorizationService.AuthorizeAsync(profile, new AbacResourceContext("Finance", "Approve", invoice.CreatedByUserId, null, true), cancellationToken);
        if (!abacResult.Allowed)
        {
            return (false, "Forbidden");
        }

        invoice.WorkflowStatus = WorkflowStatus.Rejected;
        await _context.SaveChangesAsync(cancellationToken);
        await _workflowService.RecordDecisionAsync("Invoice", invoice.Id, 1, currentUserId, WorkflowStatus.Rejected, cancellationToken: cancellationToken);
        await _invoiceService.PublishInvoiceDecisionNotificationAsync(invoice, WorkflowStatus.Rejected, NotificationSeverity.Danger, cancellationToken);

        return (true, null);
    }

    // ── Invoice Validation ────────────────────────────────

    public async Task ValidateInvoiceAsync(FinancialInvoiceUpsertVM model, Action<string, string> addModelError, CancellationToken cancellationToken, int? currentInvoiceId = null)
    {
        if (model.InvoiceType != "Sale" && model.InvoiceType != "Purchase")
        {
            addModelError(nameof(model.InvoiceType), "نوع فاکتور باید Sale یا Purchase باشد.");
        }

        if (!TryParseShamsiDate(model.DateShamsi, out _))
        {
            addModelError(nameof(model.DateShamsi), "فرمت تاریخ شمسی نامعتبر است.");
        }

        var normalizedNumber = model.InvoiceNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedNumber))
        {
            return;
        }

        if (model.InvoiceType == "Sale" && !model.EmployerId.HasValue)
        {
            addModelError(nameof(model.EmployerId), "انتخاب کارفرما برای فاکتور فروش الزامی است.");
        }

        var duplicate = await _context.Invoices
            .AsNoTracking()
            .AnyAsync(item =>
                item.InvoiceNumber == normalizedNumber &&
                item.InvoiceType == model.InvoiceType &&
                (!currentInvoiceId.HasValue || item.Id != currentInvoiceId.Value),
                cancellationToken);

        if (duplicate)
        {
            addModelError(nameof(model.InvoiceNumber), "شماره فاکتور برای این نوع، تکراری است.");
        }

        var validItems = model.Items.Where(item => !string.IsNullOrWhiteSpace(item.ItemName) && item.Quantity > 0).ToList();
        if (validItems.Count == 0)
        {
            addModelError(nameof(model.Items), "حداقل یک ردیف معتبر برای فاکتور وارد کنید.");
        }

        var productIds = validItems.Where(item => item.ProductId.HasValue).Select(item => item.ProductId!.Value).Distinct().ToList();
        if (productIds.Count > 0)
        {
            var validProductIds = await _inventoryService.GetActiveProductIdsAsync(productIds, cancellationToken);
            if (validProductIds.Count != productIds.Count)
            {
                addModelError(nameof(model.Items), "برخی کالاهای انتخاب‌شده نامعتبر یا غیرفعال هستند.");
            }
        }

        if (model.InvoiceType == "Purchase")
        {
            model.EmployerId = null;
            if (!string.IsNullOrWhiteSpace(model.DeadlineDateShamsi) && !TryParseShamsiDate(model.DeadlineDateShamsi, out _))
            {
                addModelError(nameof(model.DeadlineDateShamsi), "فرمت مهلت پیگیری نامعتبر است.");
            }

            if (model.WarehouseReceiptId.HasValue)
            {
                var warehouseReceiptId = model.WarehouseReceiptId.Value;
                var receiptExists = await _inventoryService.WarehouseReceiptExistsAsync(warehouseReceiptId, cancellationToken);
                if (!receiptExists)
                {
                    addModelError(nameof(model.WarehouseReceiptId), "رسید انبار انتخاب‌شده معتبر نیست.");
                }
                else
                {
                    var receiptMappedToOtherInvoice = await _context.Invoices
                        .AsNoTracking()
                        .AnyAsync(item =>
                            item.WarehouseReceiptId == warehouseReceiptId &&
                            item.InvoiceType == "Purchase" &&
                            (!currentInvoiceId.HasValue || item.Id != currentInvoiceId.Value),
                            cancellationToken);

                    if (receiptMappedToOtherInvoice)
                    {
                        addModelError(nameof(model.WarehouseReceiptId), "این رسید انبار قبلاً به فاکتور خرید دیگری متصل شده است.");
                    }
                }
            }

            if (model.FollowUpEmployeeId.HasValue)
            {
                var employeeExists = await _officeService.IsEmployeeActiveAsync(model.FollowUpEmployeeId.Value, cancellationToken);
                if (!employeeExists)
                {
                    addModelError(nameof(model.FollowUpEmployeeId), "مسئول پیگیری انتخاب‌شده معتبر یا فعال نیست.");
                }
            }
        }
        else if (model.InvoiceType == "Sale" && model.EmployerId.HasValue)
        {
            var employerExists = await _context.Employers
                .AsNoTracking()
                .AnyAsync(item => item.Id == model.EmployerId.Value && item.IsActive, cancellationToken);
            if (!employerExists)
            {
                addModelError(nameof(model.EmployerId), "کارفرمای انتخابی معتبر یا فعال نیست.");
            }
        }
    }

    // ── Dashboard ─────────────────────────────────────────

    public async Task<FinancialDashboardVM> BuildDashboardAsync(int? year, Func<string, string?> urlHelper, CancellationToken cancellationToken)
    {
        var targetYear = year ?? GetCurrentShamsiYear();
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(item => item.DateShamsi.StartsWith(targetYear.ToString()))
            .ToListAsync(cancellationToken);

        var totalRevenue = invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.GrandTotal);
        var totalPurchaseCost = invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.GrandTotal);
        var pendingSalesInvoices = invoices.Count(item => item.InvoiceType == "Sale" && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Archived);
        var pendingPurchaseInvoices = invoices.Count(item => item.InvoiceType == "Purchase" && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Archived);
        var duePurchaseDeadlines = invoices.Count(item => item.InvoiceType == "Purchase" && item.DeadlineDateShamsi != null && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Archived);
        var validationWarnings = invoices.Count(item =>
            string.IsNullOrWhiteSpace(item.InvoiceNumber) ||
            string.IsNullOrWhiteSpace(item.PartyName) ||
            item.GrandTotal <= 0 ||
            item.VatAmount < 0);

        var payrollCost = await _context.PayrollLists
            .AsNoTracking()
            .Include(item => item.Items)
            .Where(item => item.Year == targetYear)
            .SelectMany(item => item.Items)
            .SumAsync(item => item.NetPayable, cancellationToken);

        var totalCosts = totalPurchaseCost + payrollCost;
        var netProfitLoss = totalRevenue - totalCosts;
        var monthLabels = Enumerable.Range(1, 12).Select(item => $"{item:00}").ToList();
        var revenueSeries = new List<decimal>();
        var costSeries = new List<decimal>();
        var profitSeries = new List<decimal>();

        var monthlyAggregates = await _context.Invoices
            .AsNoTracking()
            .Where(item =>
                item.DateShamsi.StartsWith(targetYear.ToString()) &&
                item.DateShamsi.Length >= 7 &&
                (item.InvoiceType == "Sale" || item.InvoiceType == "Purchase"))
            .Select(item => new
            {
                Month = item.DateShamsi.Substring(5, 2),
                Revenue = item.InvoiceType == "Sale" ? item.GrandTotal : 0m,
                Purchase = item.InvoiceType == "Purchase" ? item.GrandTotal : 0m,
                Payroll = 0m
            })
            .Concat(
                _context.PayrollItems
                    .AsNoTracking()
                    .Where(item => item.PayrollList.Year == targetYear)
                    .Select(item => new
                    {
                        Month = (item.PayrollList.Month < 10 ? "0" : "") + item.PayrollList.Month,
                        Revenue = 0m,
                        Purchase = 0m,
                        Payroll = item.NetPayable
                    }))
            .GroupBy(item => item.Month)
            .Select(group => new
            {
                Month = group.Key,
                Revenue = group.Sum(item => item.Revenue),
                Purchase = group.Sum(item => item.Purchase),
                Payroll = group.Sum(item => item.Payroll)
            })
            .ToDictionaryAsync(item => item.Month, item => item, cancellationToken);

        for (var month = 1; month <= 12; month++)
        {
            var monthKey = $"{month:00}";
            var monthRevenue = monthlyAggregates.TryGetValue(monthKey, out var aggregate) ? aggregate.Revenue : 0;
            var monthPurchase = monthlyAggregates.TryGetValue(monthKey, out aggregate) ? aggregate.Purchase : 0;
            var monthPayroll = monthlyAggregates.TryGetValue(monthKey, out aggregate) ? aggregate.Payroll : 0;
            var monthCost = monthPurchase + monthPayroll;

            revenueSeries.Add(Math.Round(monthRevenue, 2));
            costSeries.Add(Math.Round(monthCost, 2));
            profitSeries.Add(Math.Round(monthRevenue - monthCost, 2));
        }

        return new FinancialDashboardVM
        {
            PendingSalesInvoices = pendingSalesInvoices,
            PendingPurchaseInvoices = pendingPurchaseInvoices,
            DuePurchaseDeadlines = duePurchaseDeadlines,
            ValidationWarnings = validationWarnings,
            HubKpis =
            [
                new FinancialHubKpiVM { Title = "فاکتورهای فروش باز", Value = pendingSalesInvoices.ToString("N0"), Description = "فاکتورهای ثبت شده اما هنوز نهایی نشده", Tone = "success" },
                new FinancialHubKpiVM { Title = "فاکتورهای خرید باز", Value = pendingPurchaseInvoices.ToString("N0"), Description = "خریدهای در گردش یا در انتظار بررسی", Tone = "primary" },
                new FinancialHubKpiVM { Title = "سررسیدهای نزدیک", Value = duePurchaseDeadlines.ToString("N0"), Description = "مواردی که باید همین حالا پیگیری شوند", Tone = "warning" },
                new FinancialHubKpiVM { Title = "هشدارهای اعتبارسنجی", Value = validationWarnings.ToString("N0"), Description = "رکوردهای ناقص یا مشکوک", Tone = "danger" }
            ],
            QuickActions =
            [
                new FinancialQuickActionVM { Title = "فروش", Description = "ثبت و پیگیری فاکتورهای فروش", Url = urlHelper("Sales") ?? "#", Icon = "bi-graph-up-arrow", Tone = "success" },
                new FinancialQuickActionVM { Title = "خرید", Description = "ثبت و پیگیری فاکتورهای خرید", Url = urlHelper("Purchases") ?? "#", Icon = "bi-cart", Tone = "primary" },
                new FinancialQuickActionVM { Title = "مالیات بر ارزش افزوده", Description = "کنترل مالیات بر ارزش افزوده و مغایرت‌ها", Url = urlHelper("VatDashboard") ?? "#", Icon = "bi-percent", Tone = "warning" },
                new FinancialQuickActionVM { Title = "معاملات فصلی", Description = "گزارش ماده 169 و خروجی‌ها", Url = urlHelper("SeasonalTax") ?? "#", Icon = "bi-journal-text", Tone = "info" },
                new FinancialQuickActionVM { Title = "کدینگ حساب‌ها", Description = "مدیریت سرفصل، معین و تفصیلی حساب‌ها", Url = urlHelper("ChartOfAccounts") ?? "#", Icon = "bi-diagram-2", Tone = "info" },
                new FinancialQuickActionVM { Title = "دفاتر حسابداری", Description = "دفتر کل، معین و تفصیلی", Url = urlHelper("Ledger") ?? "#", Icon = "bi-journal-arrow-down", Tone = "dark" },
                new FinancialQuickActionVM { Title = "حقوق", Description = "ورود به مدیریت حقوق و دستمزد", Url = "/Payroll/Index", Icon = "bi-cash-stack", Tone = "secondary" },
                new FinancialQuickActionVM { Title = "بیمه", Description = "ورود به مدیریت بیمه و لیست‌ها", Url = "/Bimeh/Index", Icon = "bi-shield-lock", Tone = "dark" }
            ],
            TotalRevenue = Math.Round(totalRevenue, 2),
            TotalPurchaseCost = Math.Round(totalPurchaseCost, 2),
            TotalPayrollCost = Math.Round(payrollCost, 2),
            TotalCosts = Math.Round(totalCosts, 2),
            NetProfitLoss = Math.Round(netProfitLoss, 2),
            MonthLabels = monthLabels,
            RevenueSeries = revenueSeries,
            CostSeries = costSeries,
            ProfitSeries = profitSeries,
            TotalVatCollected = Math.Round(invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.VatAmount), 2),
            TotalVatPaid = Math.Round(invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.VatAmount), 2),
            NetVatPayableOrRefundable = Math.Round(invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.VatAmount) - invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.VatAmount), 2),
            TotalVatLineCalculated = Math.Round(invoices.SelectMany(item => item.Items).Sum(item => item.LineVatAmount), 2),
            VatReconciliationDifference = Math.Round(invoices.Sum(item => item.VatAmount) - invoices.SelectMany(item => item.Items).Sum(item => item.LineVatAmount), 2),
            VatMismatchInvoices = invoices.Count(item => item.Items.Any() && Math.Round(item.Items.Sum(row => row.LineVatAmount), 2) != Math.Round(item.VatAmount, 2)),
            MonthlySalesTotal = Math.Round(invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.GrandTotal), 2),
            MonthlyPurchaseTotal = Math.Round(invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.GrandTotal), 2),
            MonthlyTaxPayable = Math.Round(invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.VatAmount) - invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.VatAmount), 2),
            OverdueInvoices = invoices.Count(item => !string.IsNullOrWhiteSpace(item.DeadlineDateShamsi) && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Archived),
            VatRows = invoices.OrderByDescending(item => item.DateShamsi).ThenByDescending(item => item.CreatedAt).Select(item => new VatInvoiceRowVM
            {
                Id = item.Id,
                InvoiceNumber = item.InvoiceNumber,
                InvoiceType = item.InvoiceType,
                DateShamsi = item.DateShamsi,
                PartyName = item.PartyName,
                SubTotal = item.SubTotal,
                VatAmount = item.VatAmount,
                GrandTotal = item.GrandTotal
            }).ToList(),
            RecentActivities = invoices.OrderByDescending(item => item.CreatedAt).Take(8).Select(item => new FinancialActivityVM
            {
                Title = $"{item.InvoiceNumber} - {item.PartyName}",
                Subtitle = $"{(item.InvoiceType == "Sale" ? "فروش" : "خرید")} | {WorkflowStatus.Label(item.WorkflowStatus)} | {item.DateShamsi}",
                Url = urlHelper("EditInvoice") != null ? $"{urlHelper("EditInvoice")}?id={item.Id}" : "#",
                Badge = item.GrandTotal.ToString("N0"),
                BadgeTone = item.InvoiceType == "Sale" ? "success" : "primary"
            }).ToList()
        };
    }

    // ── Ledger Operations ─────────────────────────────────

    public async Task<FinanceLedgerOperationsVM> BuildLedgerOperationsModelAsync(
        CancellationToken cancellationToken)
    {
        var accessTokens = await GetCurrentAccessTokensAsync(cancellationToken);
        var visibleColumns = _tableSchemaRegistry
            .GetAllowedColumns("Finance_Vouchers", accessTokens)
            .Select(item => item.ColumnId)
            .ToList();

        var currencies = await _context.Currencies
            .AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);

        var accounts = await _context.SubsidiaryAccounts
            .AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code)
            .Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.Code + " - " + item.Name })
            .ToListAsync(cancellationToken);

        var journals = await _context.JournalTypes
            .AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code)
            .Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.Code + " - " + item.Name })
            .ToListAsync(cancellationToken);

        if (journals.Count == 0)
        {
            var journal = await EnsureDefaultJournalTypeAsync(cancellationToken);
            journals.Add(new SelectListItem { Value = journal.Id.ToString(), Text = journal.Code + " - " + journal.Name });
        }

        var defaultClosingDestinationAccountId = await _context.SubsidiaryAccounts
            .AsNoTracking().Where(item => item.IsActive && item.SystemKey == FinanceAccountKeys.RetainedEarnings)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var currentFiscalYear = await _context.FiscalYears
            .AsNoTracking().Where(item => item.StartDate <= DateTime.Today && item.EndDate >= DateTime.Today)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        var periodQuery = _context.FiscalPeriods.AsNoTracking().AsQueryable();
        if (currentFiscalYear != null)
        {
            periodQuery = periodQuery.Where(item => item.FiscalYearId == currentFiscalYear.Id);
        }

        var currentPeriodId = currentFiscalYear == null ? null
            : await _context.FiscalPeriods.AsNoTracking()
                .Where(item => item.FiscalYearId == currentFiscalYear.Id && item.StartDate <= DateTime.Today && item.EndDate >= DateTime.Today)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

        return new FinanceLedgerOperationsVM
        {
            FiscalPeriods = await periodQuery.OrderBy(item => item.PeriodNumber)
                .Select(item => new FiscalPeriodRowVM { Id = item.Id, Name = item.Name, PeriodNumber = item.PeriodNumber, StartDate = item.StartDate, EndDate = item.EndDate, Status = item.Status })
                .ToListAsync(cancellationToken),
            ExchangeRates = await _context.CurrencyExchangeRates.AsNoTracking().Include(item => item.Currency)
                .OrderByDescending(item => item.RateDate).ThenBy(item => item.Currency.Code).Take(20)
                .Select(item => new CurrencyRateRowVM { CurrencyCode = item.Currency.Code, RateDate = item.RateDate, BuyRate = item.BuyRate, SellRate = item.SellRate })
                .ToListAsync(cancellationToken),
            Vouchers = await _context.VoucherHeaders.AsNoTracking().Include(item => item.JournalType).Include(item => item.Lines).ThenInclude(item => item.FloatingDetailAccount)
                .OrderByDescending(item => item.VoucherDate).ThenByDescending(item => item.Id).Take(50)
                .Select(item => new VoucherGridRowVM { Id = item.Id, SequenceNumber = item.SequenceNumber, VoucherNumber = item.VoucherNumber, VoucherDate = item.VoucherDate, Description = item.Description ?? string.Empty, JournalType = item.JournalType.Code, TotalAmount = item.TotalDebits, Status = item.Status, PostingStatus = item.PostingStatus })
                .ToListAsync(cancellationToken),
            VisibleVoucherColumns = visibleColumns,
            CurrentFiscalPeriodId = currentPeriodId,
            CurrencyOptions = currencies.Where(item => !item.IsBaseCurrency).Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.Code + " - " + item.Name }).ToList(),
            CurrencyLookupOptions = currencies.Where(item => !item.IsBaseCurrency).Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.Code + " - " + item.Name }).ToList(),
            AccountOptions = accounts,
            JournalTypeOptions = journals,
            ExchangeRate = new CurrencyExchangeRateCreateVM { RateDate = DateTime.Today, CurrencyId = currencies.FirstOrDefault(item => !item.IsBaseCurrency)?.Id ?? Guid.Empty },
            Voucher = new SimpleVoucherCreateVM { VoucherDate = DateTime.Today, JournalTypeId = int.TryParse(journals.FirstOrDefault()?.Value, out var journalId) ? journalId : 0 },
            PeriodClosing = new PeriodClosingRequestVM { FiscalPeriodId = currentPeriodId ?? Guid.Empty, DestinationAccountId = defaultClosingDestinationAccountId }
        };
    }

    public async Task<TrialBalancePageVM> BuildTrialBalancePageModelAsync(
        Guid? fiscalPeriodId, bool includeMoatagh, bool groupByFloatingDetail, bool sixColumn, CancellationToken cancellationToken)
    {
        var periods = await _context.FiscalPeriods.AsNoTracking().Include(item => item.FiscalYear)
            .OrderByDescending(item => item.StartDate).ThenByDescending(item => item.PeriodNumber)
            .Select(item => new { item.Id, item.Name, item.StartDate, item.EndDate, FiscalYearName = item.FiscalYear.YearName })
            .ToListAsync(cancellationToken);

        var selectedPeriodId = fiscalPeriodId.GetValueOrDefault();
        if (selectedPeriodId == Guid.Empty)
        {
            selectedPeriodId = periods.Where(item => item.StartDate.Date <= DateTime.Today && item.EndDate.Date >= DateTime.Today).Select(item => item.Id).FirstOrDefault();
        }
        if (selectedPeriodId == Guid.Empty)
        {
            selectedPeriodId = periods.Select(item => item.Id).FirstOrDefault();
        }

        var report = selectedPeriodId == Guid.Empty
            ? new TrialBalanceDto()
            : await _trialBalanceService.GetTrialBalanceAsync(selectedPeriodId, includeMoatagh, groupByFloatingDetail, cancellationToken);

        return new TrialBalancePageVM
        {
            FiscalPeriodId = selectedPeriodId,
            IncludeMoatagh = includeMoatagh,
            GroupByFloatingDetail = groupByFloatingDetail,
            SixColumn = sixColumn,
            FiscalPeriodOptions = periods.OrderBy(item => item.StartDate).ThenBy(item => item.Name)
                .Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.FiscalYearName + " - " + item.Name, Selected = item.Id == selectedPeriodId }).ToList(),
            Report = report
        };
    }

    public async Task EnsureCurrentFiscalYearAsync(CancellationToken cancellationToken)
    {
        var year = GetCurrentShamsiYear();
        var yearName = year.ToString(CultureInfo.InvariantCulture);
        if (await _context.FiscalYears.AnyAsync(item => item.YearName == yearName, cancellationToken))
        {
            return;
        }

        var persianCalendar = new PersianCalendar();
        var fiscalYear = new FiscalYear
        {
            YearName = yearName,
            StartDate = persianCalendar.ToDateTime(year, 1, 1, 0, 0, 0, 0),
            EndDate = persianCalendar.ToDateTime(year, 12, persianCalendar.GetDaysInMonth(year, 12), 23, 59, 59, 999),
            FiscalPeriods = Enumerable.Range(1, 12).Select(month =>
            {
                var days = persianCalendar.GetDaysInMonth(year, month);
                return new FiscalPeriod
                {
                    Name = $"{yearName}/{month:00}",
                    PeriodNumber = month,
                    StartDate = persianCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0),
                    EndDate = persianCalendar.ToDateTime(year, month, days, 23, 59, 59, 999),
                    Status = FiscalPeriodStatus.Open
                };
            }).ToList()
        };

        _context.FiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ── Export ────────────────────────────────────────────

    public async Task<List<Invoice>> GetInvoicesForExportAsync(string? invoiceType, int? year, int? quarter, CancellationToken cancellationToken)
    {
        var query = _context.Invoices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(invoiceType))
        {
            query = query.Where(item => item.InvoiceType == invoiceType);
        }

        if (year.HasValue)
        {
            query = query.Where(item => item.DateShamsi.StartsWith(year.Value.ToString()));
        }

        if (quarter is >= 1 and <= 4)
        {
            var quarterTokens = GetQuarterMonthTokens(quarter.Value);
            query = query.Where(item => quarterTokens.Any(token => item.DateShamsi.Contains(token)));
        }

        return await query.OrderByDescending(item => item.DateShamsi).ToListAsync(cancellationToken);
    }

    // ── Seasonal Tax ──────────────────────────────────────

    public async Task<SeasonalTaxReportVM> BuildSeasonalTaxReportAsync(int? year, int? quarter, CancellationToken cancellationToken)
    {
        var targetYear = year ?? GetCurrentShamsiYear();
        var targetQuarter = quarter is >= 1 and <= 4 ? quarter.Value : 1;

        var rows = await _context.Invoices.AsNoTracking()
            .Where(item =>
                item.DateShamsi.StartsWith(targetYear.ToString()) &&
                GetQuarterMonthTokens(targetQuarter).Any(token => item.DateShamsi.Contains(token)) &&
                (item.InvoiceType == "Sale" || item.InvoiceType == "Purchase"))
            .OrderBy(item => item.DateShamsi)
            .Select(item => new SeasonalTaxReportRowVM
            {
                PartyName = item.PartyName,
                NationalId = item.NationalCodeOrEconomicId,
                TransactionType = item.InvoiceType == "Sale" ? "فروش" : "خرید",
                Amount = item.SubTotal,
                Vat = item.VatAmount,
                DateShamsi = item.DateShamsi
            })
            .ToListAsync(cancellationToken);

        return new SeasonalTaxReportVM
        {
            Year = targetYear,
            Quarter = targetQuarter,
            QuarterTitle = GetQuarterTitle(targetQuarter),
            Rows = rows,
            TotalAmount = rows.Sum(item => item.Amount),
            TotalVat = rows.Sum(item => item.Vat)
        };
    }

    // ── Populate Dropdowns ────────────────────────────────

    public async Task PopulateProductOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
    {
        options.Clear();
        var items = await _inventoryService.GetActiveProductOptionsAsync(cancellationToken);
        options.AddRange(items.Select(item => new SelectListItem { Value = item.Id.ToString(), Text = $"{item.Name} ({item.Code})" }));
    }

    public async Task PopulatePurchaseOptionsAsync(FinancialInvoiceUpsertVM model, CancellationToken cancellationToken)
    {
        model.WarehouseReceiptOptions.Clear();
        model.FollowUpEmployeeOptions.Clear();

        var mappedReceiptIds = await _context.Invoices.AsNoTracking()
            .Where(item => item.InvoiceType == "Purchase" && item.WarehouseReceiptId.HasValue && (!model.Id.HasValue || item.Id != model.Id.Value))
            .Select(item => item.WarehouseReceiptId!.Value)
            .ToListAsync(cancellationToken);

        var receiptItems = await _inventoryService.GetAvailableWarehouseReceiptOptionsAsync(mappedReceiptIds, model.WarehouseReceiptId, cancellationToken);
        model.WarehouseReceiptOptions.AddRange(receiptItems.Select(item => new SelectListItem { Value = item.Id.ToString(), Text = $"{item.ReceiptNumber} - {item.SupplierOrSource} ({item.DateShamsi})" }));

        var employeeItems = await _officeService.GetActiveEmployeeOptionsAsync(cancellationToken);
        model.FollowUpEmployeeOptions.AddRange(employeeItems.Select(item => new SelectListItem { Value = item.Id.ToString(), Text = $"{item.FullName} ({item.PersonnelCode})" }));
    }

    public async Task PopulateEmployerOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
    {
        options.Clear();
        var employers = await _context.Employers.AsNoTracking()
            .Where(item => item.IsActive).OrderBy(item => item.Name)
            .Select(item => new SelectListItem { Value = item.Id.ToString(), Text = string.IsNullOrWhiteSpace(item.ContractNumber) ? item.Name : $"{item.Name} ({item.ContractNumber})" })
            .ToListAsync(cancellationToken);
        options.AddRange(employers);
    }

    // ── Helpers ───────────────────────────────────────────

    public async Task<string> BuildNextInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var count = await _context.Invoices.CountAsync(cancellationToken);
        return $"FIN-{count + 1:00000}";
    }

    public static string GetTodayShamsi()
    {
        var persianCalendar = new PersianCalendar();
        var now = DateTime.Now;
        return $"{persianCalendar.GetYear(now):0000}/{persianCalendar.GetMonth(now):00}/{persianCalendar.GetDayOfMonth(now):00}";
    }

    public static int GetCurrentShamsiYear()
    {
        return new PersianCalendar().GetYear(DateTime.Now);
    }

    public async Task<object?> GetReceiptItemsJsonAsync(int receiptId, CancellationToken cancellationToken)
    {
        var receipt = await _inventoryService.GetReceiptDetailsAsync(receiptId, cancellationToken);
        if (receipt == null)
        {
            return null;
        }

        return new
        {
            success = true,
            receiptId = receipt.Id,
            receiptNumber = receipt.ReceiptNumber,
            items = receipt.Items.Select(item => new
            {
                productId = item.ProductId,
                itemName = item.ItemName,
                quantity = item.Quantity,
                unitPrice = item.UnitPrice,
                lineSubTotal = Math.Round(item.Quantity * item.UnitPrice, 2),
                lineVatAmount = Math.Round(item.Quantity * item.UnitPrice * 0.10m, 2),
                lineGrandTotal = Math.Round(item.Quantity * item.UnitPrice * 1.10m, 2)
            }).ToList()
        };
    }

    public async Task<Invoice?> GetInvoiceForPrintAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Invoices.AsNoTracking().Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<object?> GetWarehouseReceiptForPrintAsync(int id, CancellationToken cancellationToken)
    {
        return await _inventoryService.GetWarehouseReceiptForPrintAsync(id, cancellationToken);
    }

    public async Task<(PayrollList? Payroll, PayrollItem? Item)> GetPayslipForPrintAsync(int payrollListId, int payrollItemId, CancellationToken cancellationToken)
    {
        var payroll = await _context.PayrollLists.AsNoTracking().Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == payrollListId, cancellationToken);
        if (payroll == null)
        {
            return (null, null);
        }

        var payrollItem = payroll.Items.FirstOrDefault(item => item.Id == payrollItemId);
        return (payroll, payrollItem);
    }

    public async Task<object?> GetLatestCurrencyRateAsync(Guid currencyId, DateTime? rateDate, CancellationToken cancellationToken)
    {
        var date = (rateDate ?? DateTime.Today).Date;
        var rate = await _context.CurrencyExchangeRates.AsNoTracking()
            .Where(item => item.CurrencyId == currencyId && item.RateDate <= date)
            .OrderByDescending(item => item.RateDate)
            .Select(item => new { item.SellRate, item.BuyRate, item.RateDate })
            .FirstOrDefaultAsync(cancellationToken);
        return rate == null
            ? new { found = false, exchangeRate = 1m }
            : new { found = true, exchangeRate = rate.SellRate, rateDate = rate.RateDate.ToString("yyyy-MM-dd") };
    }

    public async Task<List<object>> GetFloatingDetailsForSubsidiaryAccountAsync(int subsidiaryAccountId, CancellationToken cancellationToken)
    {
        if (subsidiaryAccountId <= 0)
        {
            return new List<object>();
        }

        var allowsFloatingDetail = await _context.SubsidiaryAccounts.AsNoTracking()
            .Where(item => item.Id == subsidiaryAccountId && item.IsActive)
            .Select(item => item.AllowsFloatingDetail)
            .FirstOrDefaultAsync(cancellationToken);

        if (!allowsFloatingDetail)
        {
            return new List<object>();
        }

        return await _context.SubsidiaryAccountFloatingDetails.AsNoTracking()
            .Where(item => item.SubsidiaryAccountId == subsidiaryAccountId && item.FloatingDetailAccount.IsActive)
            .OrderBy(item => item.FloatingDetailAccount.Code)
            .Select(item => new
            {
                id = item.FloatingDetailAccountId.ToString(),
                text = item.FloatingDetailAccount.Code + " - " + item.FloatingDetailAccount.Name,
                code = item.FloatingDetailAccount.Code,
                name = item.FloatingDetailAccount.Name
            })
            .ToListAsync<object>(cancellationToken);
    }

    public async Task<List<object>> GetSubsidiaryAccountsAsync(string? term, CancellationToken cancellationToken)
    {
        var normalizedTerm = (term ?? string.Empty).Trim();
        var query = _context.SubsidiaryAccounts.AsNoTracking().Where(item => item.IsActive);

        if (!string.IsNullOrWhiteSpace(normalizedTerm))
        {
            query = query.Where(item => item.Code.Contains(normalizedTerm) || item.Name.Contains(normalizedTerm));
        }

        return await query.OrderBy(item => item.Code).Take(40)
            .Select(item => new { id = item.Id, text = item.Code + " - " + item.Name, code = item.Code, name = item.Name })
            .ToListAsync<object>(cancellationToken);
    }

    // ── Static Helpers ────────────────────────────────────

    public static List<string> GetQuarterMonthTokens(int quarter)
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

    public static string GetQuarterTitle(int quarter)
    {
        return quarter switch { 1 => "بهار", 2 => "تابستان", 3 => "پاییز", _ => "زمستان" };
    }

    public static string? NormalizeOptionalShamsi(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }
        return PersianTextNormalizer.Normalize(input);
    }

    public static bool TryParseShamsiDate(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = PersianTextNormalizer.Normalize(value).Replace('-', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var year) ||
            !int.TryParse(parts[1], out var month) ||
            !int.TryParse(parts[2], out var day))
        {
            return false;
        }

        try
        {
            var persianCalendar = new PersianCalendar();
            result = persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Private Helpers ───────────────────────────────────

    private async Task<IReadOnlyCollection<string>> GetCurrentAccessTokensAsync(CancellationToken cancellationToken)
    {
        var profile = await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (profile != null)
        {
            foreach (var role in profile.Roles) { tokens.Add(role); }
            foreach (var permission in profile.Permissions) { tokens.Add(permission); }
        }

        return tokens;
    }

    private async Task<JournalType> EnsureDefaultJournalTypeAsync(CancellationToken cancellationToken)
    {
        var journal = await _context.JournalTypes.FirstOrDefaultAsync(item => item.Code == JournalTypeCodes.General, cancellationToken);
        if (journal != null)
        {
            return journal;
        }

        journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal", IsActive = true };
        _context.JournalTypes.Add(journal);
        await _context.SaveChangesAsync(cancellationToken);
        return journal;
    }

    private async Task<string> ResolveEmployerNameAsync(int? employerId, string? fallbackName, CancellationToken cancellationToken)
    {
        if (employerId.HasValue)
        {
            var employerName = await _context.Employers.AsNoTracking()
                .Where(item => item.Id == employerId.Value)
                .Select(item => item.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(employerName))
            {
                return employerName;
            }
        }
        return string.IsNullOrWhiteSpace(fallbackName) ? "نامشخص" : fallbackName.Trim();
    }
}