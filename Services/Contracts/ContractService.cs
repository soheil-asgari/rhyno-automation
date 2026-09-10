using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Services.Contracts;

public sealed class ContractService : IContractService
{
    private readonly OfficeDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ContractService> _logger;
    private readonly IAuditLogger _auditLogger;
    private readonly ITemplateRenderingService _templateRenderer;
    private readonly ITenantSettingsService _tenantSettings;
    private readonly IVersionService _versionService;
    private readonly IContractAccountingService _contractAccounting;
    private readonly IPaymentPenaltyService _penaltyService;
    private readonly IDocumentRequirementService _documentRequirementService;

    public ContractService(
        OfficeDbContext context,
        UserManager<User> userManager,
        ILogger<ContractService> logger,
        IAuditLogger auditLogger,
        ITemplateRenderingService templateRenderer,
        ITenantSettingsService tenantSettings,
        IVersionService versionService,
        IContractAccountingService contractAccounting,
        IPaymentPenaltyService penaltyService,
        IDocumentRequirementService documentRequirementService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _auditLogger = auditLogger;
        _templateRenderer = templateRenderer;
        _tenantSettings = tenantSettings;
        _versionService = versionService;
        _contractAccounting = contractAccounting;
        _penaltyService = penaltyService;
        _documentRequirementService = documentRequirementService;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private async Task<bool> IsAdminAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;
        return await _userManager.IsInRoleAsync(user, "Admin");
    }

    /// <summary>
    /// Enforces the Contract status state machine. Invalid transitions fail safely.
    /// Non-admin users may only move a contract forward through the standard lifecycle.
    /// Also checks mandatory document requirements for Draft→PendingApproval transitions.
    /// </summary>
    private async Task ValidateContractStateTransition(string currentStatus, string requestedStatus, bool isAdmin, string currentUserId, int? contractId = null)
    {
        // No transition requested / same state — always allowed.
        if (string.Equals(currentStatus, requestedStatus, StringComparison.Ordinal))
            return;

        // Admin can perform any transition (business gate lives elsewhere).
        if (isAdmin)
            return;

        var allowedTransitions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [ContractStatuses.Draft] = new() { ContractStatuses.PendingApproval, ContractStatuses.Cancelled },
            [ContractStatuses.PendingApproval] = new() { ContractStatuses.Draft, ContractStatuses.Active, ContractStatuses.Cancelled, ContractStatuses.Terminated },
            [ContractStatuses.Active] = new() { ContractStatuses.Completed, ContractStatuses.Expired, ContractStatuses.Terminated, ContractStatuses.Cancelled, ContractStatuses.ExpiringSoon },
            [ContractStatuses.ExpiringSoon] = new() { ContractStatuses.Active, ContractStatuses.Expired, ContractStatuses.Completed, ContractStatuses.Terminated },
            [ContractStatuses.Suspended] = new() { ContractStatuses.Active, ContractStatuses.Terminated },
            [ContractStatuses.Expired] = new() { ContractStatuses.Completed, ContractStatuses.Terminated, ContractStatuses.Active },
            [ContractStatuses.Completed] = new() { ContractStatuses.Active, ContractStatuses.Terminated },
            [ContractStatuses.Cancelled] = new() { ContractStatuses.Draft },
            [ContractStatuses.Terminated] = new() { },
            [ContractStatuses.Archived] = new() { }
        };

        if (allowedTransitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(requestedStatus))
            return;

        throw new UnauthorizedAccessException(
            $"انتقال وضعیت از «{currentStatus}» به «{requestedStatus}» مجاز نیست.");
    }

    private async Task EnforceDocumentRequirementsAsync(int contractId)
    {
        var (allUploaded, missingNames) = await _documentRequirementService.ValidateRequiredDocumentsAsync(contractId);
        if (!allUploaded)
        {
            var missingStr = string.Join("، ", missingNames);
            throw new InvalidOperationException(
                $"برای انتقال وضعیت، ابتدا مدارک الزامی زیر را آپلود کنید: {missingStr}");
        }
    }

    /// <summary>
    /// Base query that enforces visibility: Admins see all; assigned/creator users see their own.
    /// </summary>
    private IQueryable<Contract> ApplyVisibilityFilter(IQueryable<Contract> query, string currentUserId, bool isAdmin)
    {
        if (isAdmin) return query;
        return query.Where(c => c.AssignedUserId == currentUserId || c.CreatedByUserId == currentUserId);
    }

    /// <summary>
    /// Enforce access control - throws KeyNotFoundException if user cannot access this contract
    /// </summary>
    public async Task EnforceAccessAsync(int contractId, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        var contract = await _context.Contracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null)
            throw new KeyNotFoundException($"Contract {contractId} not found.");

        if (!CanUserViewContract(contract, currentUserId, isAdmin))
            throw new UnauthorizedAccessException("شما به این قرارداد دسترسی ندارید.");
    }

    private async Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetUserSelectListAsync(string? selectedUserId, CancellationToken ct)
    {
        return await _userManager.Users.OrderBy(u => u.FullName)
            .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u.Id, Text = u.FullName ?? u.UserName ?? u.Email ?? u.Id, Selected = u.Id == selectedUserId })
            .ToListAsync(ct);
    }

    private async Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetTemplateSelectListAsync(string currentUserId, int? selectedTemplateId, CancellationToken ct)
    {
        // Show both global templates (OwnerUserId == null) and personal templates
        return await _context.ContractTemplates.AsNoTracking()
            .Where(t => t.OwnerUserId == null || t.OwnerUserId == currentUserId)
            .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.Name + (t.IsDefault ? " (پیش‌فرض)" : "") + (t.OwnerUserId == null ? " [عمومی]" : ""),
                Selected = t.Id == selectedTemplateId
            })
            .ToListAsync(ct);
    }

    private async Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetCustomerSelectListAsync(int? selectedCustomerId, CancellationToken ct)
    {
        return await _context.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = c.Id == selectedCustomerId
            }).ToListAsync(ct);
    }

    /// <summary>
    /// محاسبه و به‌روزرسانی وضعیت پرداخت قرارداد
    /// </summary>
    public async Task RecalculatePaymentStatusAsync(int contractId, CancellationToken ct = default)
    {
        var contract = await _context.Contracts
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null) return;

        contract.PaidAmount = contract.Payments.Sum(p => p.Amount);
        contract.RemainingBalance = contract.TotalAmount - contract.PaidAmount;

        if (contract.RemainingBalance <= 0 && contract.TotalAmount > 0)
            contract.PaymentStatus = PaymentStatuses.FullyPaid;
        else if (contract.PaidAmount > 0)
            contract.PaymentStatus = PaymentStatuses.PartiallyPaid;
        else
            contract.PaymentStatus = PaymentStatuses.Unpaid;

        contract.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// به‌روزرسانی وضعیت قراردادهای در شرف انقضا
    /// </summary>
    public async Task<int> UpdateExpiringSoonStatusesAsync(CancellationToken ct = default)
    {
        var now = DateTime.Today;
        var contracts = await _context.Contracts
            .Where(c => c.Status == ContractStatuses.Active && !c.IsArchived && c.EndDate != null)
            .ToListAsync(ct);

        int updated = 0;
        foreach (var contract in contracts)
        {
            var daysRemaining = (contract.EndDate!.Value - now).Days;
            if (daysRemaining >= 0 && daysRemaining <= contract.ExpiryNoticeDays)
            {
                if (contract.Status != ContractStatuses.ExpiringSoon)
                {
                    contract.Status = ContractStatuses.ExpiringSoon;
                    contract.UpdatedAt = DateTime.Now;
                    updated++;
                }
            }
            else if (daysRemaining < 0)
            {
                if (contract.Status != ContractStatuses.Expired)
                {
                    contract.Status = ContractStatuses.Expired;
                    contract.UpdatedAt = DateTime.Now;
                    updated++;
                }
            }
        }

        if (updated > 0)
            await _context.SaveChangesAsync(ct);

        return updated;
    }

    /// <summary>
    /// ثبت لاگ تغییرات در قرارداد
    /// </summary>
    public async Task AddAuditLogAsync(int contractId, string userId, string action, string? oldStatus, string? newStatus, string? description = null, CancellationToken ct = default)
    {
        try
        {
            var auditLog = new ContractAuditLog
            {
                ContractId = contractId,
                UserId = userId,
                Action = action,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Description = description,
                CreatedAt = DateTime.Now
            };
            _context.ContractAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(ct);
            _logger.LogDebug("Audit log created: ContractId={ContractId}, Action={Action}, UserId={UserId}", contractId, action, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for contract {ContractId}: Action={Action}", contractId, action);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Contract CRUD
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractIndexVM> GetIndexViewModelAsync(ContractIndexVM filter, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);

        var query = _context.Contracts.AsNoTracking()
            .Include(c => c.AssignedUser)
            .Include(c => c.Customer)
            .Include(c => c.Payments)
            .AsQueryable();

        // Visibility enforcement
        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);

        // Archived filter
        if (!filter.ShowArchived) query = query.Where(c => !c.IsArchived);

        // Search
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(c => c.Title.Contains(term) || c.ContractNumber.Contains(term) || c.ClientName.Contains(term));
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(c => c.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.AssignedUserId)) query = query.Where(c => c.AssignedUserId == filter.AssignedUserId);
        if (!string.IsNullOrWhiteSpace(filter.ClientName)) query = query.Where(c => c.ClientName.Contains(filter.ClientName));
        if (!string.IsNullOrWhiteSpace(filter.ContractType)) query = query.Where(c => c.ContractType == filter.ContractType);
        if (!string.IsNullOrWhiteSpace(filter.PaymentStatus)) query = query.Where(c => c.PaymentStatus == filter.PaymentStatus);
        if (filter.StartDateFrom.HasValue) query = query.Where(c => c.StartDate >= filter.StartDateFrom.Value);
        if (filter.StartDateTo.HasValue) query = query.Where(c => c.StartDate <= filter.StartDateTo.Value);
        if (filter.EndDateFrom.HasValue) query = query.Where(c => c.EndDate >= filter.EndDateFrom.Value);
        if (filter.EndDateTo.HasValue) query = query.Where(c => c.EndDate <= filter.EndDateTo.Value);
        if (filter.CustomerId.HasValue) query = query.Where(c => c.CustomerId == filter.CustomerId.Value);
        if (filter.MinAmount.HasValue) query = query.Where(c => c.TotalAmount >= filter.MinAmount.Value);
        if (filter.MaxAmount.HasValue) query = query.Where(c => c.TotalAmount <= filter.MaxAmount.Value);
        if (filter.ExpiringSoonOnly == true) query = query.Where(c => c.Status == ContractStatuses.ExpiringSoon);

        // Payment status filters
        if (filter.PaymentStatus == PaymentStatuses.FullyPaid) query = query.Where(c => c.PaymentStatus == PaymentStatuses.FullyPaid);
        else if (filter.PaymentStatus == PaymentStatuses.Unpaid) query = query.Where(c => c.PaymentStatus == PaymentStatuses.Unpaid);
        else if (filter.PaymentStatus == PaymentStatuses.PartiallyPaid) query = query.Where(c => c.PaymentStatus == PaymentStatuses.PartiallyPaid);

        // Sorting
        query = (filter.SortBy?.ToLowerInvariant()) switch
        {
            "contractnumber" => filter.SortDescending == true ? query.OrderByDescending(c => c.ContractNumber) : query.OrderBy(c => c.ContractNumber),
            "title" => filter.SortDescending == true ? query.OrderByDescending(c => c.Title) : query.OrderBy(c => c.Title),
            "clientname" => filter.SortDescending == true ? query.OrderByDescending(c => c.ClientName) : query.OrderBy(c => c.ClientName),
            "startdate" => filter.SortDescending == true ? query.OrderByDescending(c => c.StartDate) : query.OrderBy(c => c.StartDate),
            "enddate" => filter.SortDescending == true ? query.OrderByDescending(c => c.EndDate) : query.OrderBy(c => c.EndDate),
            "totalamount" => filter.SortDescending == true ? query.OrderByDescending(c => c.TotalAmount) : query.OrderBy(c => c.TotalAmount),
            "paidamount" => filter.SortDescending == true ? query.OrderByDescending(c => c.PaidAmount) : query.OrderBy(c => c.PaidAmount),
            "status" => filter.SortDescending == true ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            _ => query.OrderByDescending(c => c.CreatedAt)
        };

        // Pagination
        var totalFiltered = await query.CountAsync(ct);
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;
        var page = filter.Page > 0 ? filter.Page : 1;
        var totalPages = (int)Math.Ceiling(totalFiltered / (double)pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ContractIndexItemVM
            {
                Id = c.Id,
                ContractNumber = c.ContractNumber,
                Title = c.Title,
                ClientName = c.ClientName,
                CustomerName = c.Customer != null ? c.Customer.Name : null,
                ContractType = c.ContractType,
                TotalAmount = c.TotalAmount,
                PaidAmount = c.PaidAmount,
                RemainingBalance = c.RemainingBalance,
                PaymentStatus = c.PaymentStatus,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                ResponsibleUserName = c.AssignedUser != null ? c.AssignedUser.FullName : null,
                AssignedUserId = c.AssignedUserId,
                IsArchived = c.IsArchived,
                IsOwnedByCurrentUser = c.AssignedUserId == currentUserId || c.CreatedByUserId == currentUserId,
                CreatedAt = c.CreatedAt,
                Payments = c.Payments.ToList()
            }).ToListAsync(ct);

        // Summary stats
        var allQuery = _context.Contracts.AsNoTracking();
        allQuery = ApplyVisibilityFilter(allQuery, currentUserId, isAdmin);

        filter.TotalCount = await allQuery.CountAsync(ct);
        filter.ActiveCount = await allQuery.CountAsync(c => c.Status == ContractStatuses.Active, ct);
        filter.CompletedCount = await allQuery.CountAsync(c => c.Status == ContractStatuses.Completed, ct);
        filter.DraftCount = await allQuery.CountAsync(c => c.Status == ContractStatuses.Draft, ct);
        filter.ExpiringSoonCount = await allQuery.CountAsync(c => c.Status == ContractStatuses.ExpiringSoon, ct);
        filter.FilteredCount = totalFiltered;
        filter.TotalAmountSum = await allQuery.SumAsync(c => c.TotalAmount, ct);
        filter.TotalPaidSum = await allQuery.SumAsync(c => c.PaidAmount, ct);
        filter.Page = page;
        filter.PageSize = pageSize;
        filter.TotalPages = totalPages;

        // Dropdown options
        filter.StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == filter.Status }).ToList();
        filter.ContractTypeOptions = ContractTypes.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == filter.ContractType }).ToList();
        filter.ResponsibleUserOptions = await GetUserSelectListAsync(filter.AssignedUserId, ct);
        filter.CustomerOptions = await GetCustomerSelectListAsync(filter.CustomerId, ct);
        filter.PaymentStatusOptions = PaymentStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s switch
        {
            "Unpaid" => "پرداخت نشده",
            "PartiallyPaid" => "پرداخت جزئی",
            "FullyPaid" => "تسویه شده",
            _ => s
        }, Selected = s == filter.PaymentStatus }).ToList();
        filter.Items = items;
        // ── Load saved column preferences ──
        var columnPrefs = await GetColumnPreferenceAsync(currentUserId, ct);
        if (columnPrefs != null)
        {
            filter.SavedColumnsJson = System.Text.Json.JsonSerializer.Serialize(columnPrefs);
        }
        return filter;
    }

    public async Task<Contract?> GetContractByIdAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        IQueryable<Contract> query = _context.Contracts.AsNoTracking()
            .Include(c => c.AssignedUser).Include(c => c.CreatedByUser)
            .Include(c => c.Customer)
            .Include(c => c.ContractTemplate).Include(c => c.Payments).Include(c => c.Attachments).Include(c => c.AuditLogs);

        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);
        return await query.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<ContractDetailsVM> GetDetailsViewModelAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        IQueryable<Contract> query = _context.Contracts.AsNoTracking()
            .Include(x => x.AssignedUser).Include(x => x.CreatedByUser)
            .Include(x => x.Customer)
            .Include(x => x.ContractTemplate)
            .Include(x => x.Payments)
            .Include(x => x.Attachments)
            .Include(x => x.AuditLogs).ThenInclude(al => al.User)
            .Include(x => x.ArchivedByUser)
            .Include(x => x.Attachments).ThenInclude(a => a.UploadedByUser)
            .Include(x => x.Amendments).ThenInclude(a => a.CreatedByUser)
            .Include(x => x.AutoRenewalLogs).ThenInclude(al => al.CancelledByUser);

        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);

        var c = await query.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Contract {id} not found.");

        // SAFE: Compute DaysRemaining only when EndDate is valid (not null, not MinValue, year >= 1700).
        // This prevents PersianCalendar / date arithmetic exceptions downstream.
        int daysRemaining = 0;
        if (c.EndDate.HasValue && c.EndDate.Value != DateTime.MinValue && c.EndDate.Value.Year >= 1700)
        {
            daysRemaining = (c.EndDate.Value.Date - DateTime.Today).Days;
        }
        else
        {
            daysRemaining = 0; // No valid end date → no remaining days to display
        }

        var signers = await _context.ContractSignatureSigners
            .Where(s => s.SignatureRequest != null && s.SignatureRequest.ContractId == c.Id && !string.IsNullOrEmpty(s.SignatureImageData))
            .OrderByDescending(s => s.SignedAt)
            .ToListAsync(ct);

        var firstPartySigner = signers.FirstOrDefault(s => s.JobTitle == "FirstParty" || (s.SignerType == SignatureSignerType.Internal && s.JobTitle != "SecondParty"));
        var secondPartySigner = signers.FirstOrDefault(s => s.JobTitle == "SecondParty" || s.SignerType == SignatureSignerType.External);
        var latestSigner = signers.FirstOrDefault();

        string? signerPartyDisplay = null;
        if (latestSigner != null)
        {
            signerPartyDisplay = latestSigner.JobTitle == "SecondParty"
                ? $"طرف دوم (مشتری / {c.Customer?.Name ?? c.ClientName ?? "مشتری"})"
                : "طرف اول (شرکت)";
        }

        return new ContractDetailsVM
        {
            Id = c.Id,
            ContractNumber = c.ContractNumber,
            Title = c.Title,
            ContractType = c.ContractType,
            ClientName = c.ClientName ?? string.Empty,
            ClientPhone = c.ClientPhone,
            ClientAddress = c.ClientAddress,
            CustomerId = c.CustomerId,
            CustomerName = c.Customer?.Name,
            Description = c.Description,
            Notes = c.Notes,
            TermsAndConditions = c.TermsAndConditions,
            ContractBodyHtml = c.ContractBodyHtml,
            TotalAmount = c.TotalAmount,
            CurrencyCode = string.IsNullOrWhiteSpace(c.CurrencyCode) ? CurrencyCode.IRR : c.CurrencyCode,
            ExchangeRate = c.ExchangeRate > 0 ? c.ExchangeRate : 1m,
            TaxMethod = string.IsNullOrWhiteSpace(c.TaxMethod) ? TaxCalculationMethod.Exclusive : c.TaxMethod,
            DefaultTaxRatePercent = c.DefaultTaxRatePercent,
            TotalTaxAmount = c.TotalTaxAmount,
            TotalAmountInclusiveTax = c.TotalAmountInclusiveTax,
            PaidAmount = c.PaidAmount,
            RemainingBalance = c.RemainingBalance,
            PaymentStatus = c.PaymentStatus,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            SignDate = c.SignDate,
            ExpiryNoticeDays = c.ExpiryNoticeDays,
            Status = c.Status,
            ResponsibleUserName = c.AssignedUser?.FullName ?? c.AssignedUser?.UserName,
            AssignedUserId = c.AssignedUserId,
            TemplateName = c.ContractTemplate?.Name,
            IsArchived = c.IsArchived,
            ArchivedAt = c.ArchivedAt,
            ArchivedByUserName = c.ArchivedByUser?.FullName ?? c.ArchivedByUser?.UserName,
            CreatedByUserName = c.CreatedByUser?.FullName ?? c.CreatedByUser?.UserName ?? "نامشخص",
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            DaysRemaining = daysRemaining,
            CanEdit = CanUserEditContract(c, currentUserId, isAdmin),
            CanViewFinancials = CanUserViewFinancials(c, currentUserId, isAdmin),

            // ── Auto-Renewal ──
            IsAutoRenew = c.IsAutoRenew,
            AutoRenewDaysBeforeExpiry = c.AutoRenewDaysBeforeExpiry,
            AutoRenewTermMonths = c.AutoRenewTermMonths,
            AutoRenewalCancelledAt = c.AutoRenewalCancelledAt,
            AutoRenewalCancelledByUserName = c.AutoRenewalCancelledByUser?.FullName ?? c.AutoRenewalCancelledByUser?.UserName,
            LastAutoRenewedAt = c.LastAutoRenewedAt,
            AutoRenewalLogs = c.AutoRenewalLogs.OrderByDescending(al => al.RenewedAt).Select(al => new AutoRenewalLogVM
            {
                Id = al.Id,
                ContractId = al.ContractId,
                RenewedContractId = al.RenewedContractId,
                RenewedAt = al.RenewedAt,
                RenewedAtText = ToPersianDate(al.RenewedAt),
                PreviousEndDate = al.PreviousEndDate,
                PreviousEndDateText = al.PreviousEndDate.HasValue ? ToPersianDate(al.PreviousEndDate.Value) : null,
                NewEndDate = al.NewEndDate,
                NewEndDateText = al.NewEndDate.HasValue ? ToPersianDate(al.NewEndDate.Value) : null,
                RenewalTermMonths = al.RenewalTermMonths,
                Status = al.Status,
                Notes = al.Notes,
                CancelledByUserName = al.CancelledByUser?.FullName ?? al.CancelledByUser?.UserName
            }).ToList(),
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new ContractPaymentVM
            {
                Id = p.Id, Amount = p.Amount, PaymentDate = p.PaymentDate, DueDate = p.DueDate,
                Status = p.Status, PaymentMethod = p.PaymentMethod, ReferenceNumber = p.ReferenceNumber,
                Notes = p.Notes, CreatedAt = p.CreatedAt
            }).ToList(),
            Attachments = c.Attachments.OrderByDescending(a => a.CreatedAt).Select(a => new ContractAttachmentVM
            {
                Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, FileSize = a.FileSize, CreatedAt = a.CreatedAt,
                UploadedByUserName = a.UploadedByUser != null ? a.UploadedByUser.FullName ?? a.UploadedByUser.UserName : null
            }).ToList(),
            AuditLogs = c.AuditLogs.OrderByDescending(al => al.CreatedAt).Select(al => new ContractAuditLogVM
            {
                Id = al.Id, UserName = al.User != null ? al.User.FullName ?? al.User.UserName ?? "نامشخص" : "نامشخص",
                Action = al.Action, OldStatus = al.OldStatus, NewStatus = al.NewStatus,
                Description = al.Description, CreatedAt = al.CreatedAt
            }).ToList(),
            LifecycleSteps = BuildLifecycleSteps(c.AuditLogs, c.Status),
            Amendments = c.Amendments.OrderByDescending(a => a.CreatedAt).Select(a => new AmendmentListItemVM
            {
                Id = a.Id,
                AmendmentType = a.AmendmentType,
                AmendmentNumber = a.AmendmentNumber,
                Title = a.Title,
                Description = a.Description,
                AmountChange = a.AmountChange,
                NewTotalAmount = a.NewTotalAmount,
                EndDateChangeDays = a.EndDateChangeDays,
                NewEndDate = a.NewEndDate,
                EffectiveDate = a.EffectiveDate,
                Status = a.Status,
                CreatedByUserName = a.CreatedByUser != null ? a.CreatedByUser.FullName ?? a.CreatedByUser.UserName ?? "" : "",
                CreatedAt = a.CreatedAt,
                CreatedAtText = ToPersianDate(a.CreatedAt)
            }).ToList(),
            TypeFields = await LoadFieldValuesAsync(c.Id, ct),
            SignatureImageData = latestSigner?.SignatureImageData,
            FirstPartySignatureImageData = firstPartySigner?.SignatureImageData,
            SecondPartySignatureImageData = secondPartySigner?.SignatureImageData,
            SignerParty = latestSigner?.JobTitle,
            SignerPartyDisplay = signerPartyDisplay,
            SignerName = latestSigner?.FullName,
            SignedAt = latestSigner?.SignedAt,
            SignedAtPersian = latestSigner?.SignedAt.HasValue == true ? ToPersianDate(latestSigner.SignedAt.Value) : null
        };
    }

    /// <summary>
    /// Builds a data-driven lifecycle stepper step list from the contract's audit log entries.
    /// and descriptions. Detects rejection cycles and marks them with RejectionCount.
    /// Falls back to the contract's current Status when no audit logs exist.
    /// </summary>
    private static List<LifecycleStepVM> BuildLifecycleSteps(ICollection<ContractAuditLog> auditLogs, string? contractStatus = null)
    {
        // Canonical lifecycle order (status key → display label → icon)
        var lifecycleMap = new (string key, string label, string icon)[]
        {
            (ContractStatuses.Draft,            "پیش‌نویس",          "bi-pencil"),
            (ContractStatuses.PendingApproval,  "در انتظار تایید",   "bi-clock"),
            (ContractStatuses.Active,           "فعال",              "bi-check-circle"),
            (ContractStatuses.ExpiringSoon,     "در شرف انقضا",      "bi-exclamation-triangle"),
            (ContractStatuses.Expired,          "منقضی شده",         "bi-calendar-x"),
        };

        // Get status-transition audit logs, ordered chronologically
        var transitions = auditLogs
            .Where(al => al.Action == "StatusChanged" && al.NewStatus != null)
            .OrderBy(al => al.CreatedAt)
            .ToList();

        // Determine the most recent transition for each target status
        var latestPerStatus = transitions
            .GroupBy(al => al.NewStatus!)
            .ToDictionary(g => g.Key, g => g.Last());

        // Count how many times each status was the target (to detect rejection cycles)
        var transitionCounts = transitions
            .GroupBy(al => al.NewStatus!)
            .ToDictionary(g => g.Key, g => g.Count());

        // Determine which status is current: the last transition's NewStatus, or the contract's Status
        string? currentStatus = transitions.LastOrDefault()?.NewStatus ?? contractStatus;

        var steps = new List<LifecycleStepVM>();
        foreach (var (key, label, icon) in lifecycleMap)
        {
            var isCompleted = latestPerStatus.ContainsKey(key);
            var isCurrent = key == currentStatus;
            var count = transitionCounts.GetValueOrDefault(key, 0);
            var hasRejection = count > 1; // More than one entry = was rejected and re-entered

            ContractAuditLog? entry = null;
            latestPerStatus.TryGetValue(key, out entry);

            steps.Add(new LifecycleStepVM
            {
                StatusKey = key,
                Label = label,
                Icon = icon,
                IsCompleted = isCompleted,
                IsCurrent = isCurrent,
                CompletedAt = entry?.CreatedAt,
                CompletedByUserName = entry?.User?.FullName ?? entry?.User?.UserName,
                Description = entry?.Description,
                RejectionReason = hasRejection ? $"این مرحله {count} بار ثبت شده است (بازگشت/تکرار)." : null,
                RejectionCount = hasRejection ? count : 0,
                ApprovalDecision = null // Approval data can be hydrated separately if needed
            });
        }

        return steps;
    }

    public async Task<ContractCreateVM> BuildCreateViewModelAsync(string currentUserId, CancellationToken ct = default)
    {
        var contractNumber = await GenerateNextContractNumberAsync(ct);
        var typeFields = await GetAllTypeFieldDefinitionsAsync(ct);
        return new ContractCreateVM
        {
            ContractNumber = contractNumber,
            StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s }).ToList(),
            ContractTypeOptions = ContractTypes.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s }).ToList(),
            UserOptions = await GetUserSelectListAsync(null, ct),
            TemplateOptions = await GetTemplateSelectListAsync(currentUserId, null, ct),
            CustomerOptions = await GetCustomerSelectListAsync(null, ct),
            CurrencyOptions = CurrencyCode.All.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c,
                Text = c switch
                {
                    CurrencyCode.IRR => "ریال ایران (IRR)",
                    CurrencyCode.USD => "دلار آمریکا (USD)",
                    CurrencyCode.EUR => "یورو (EUR)",
                    CurrencyCode.GBP => "پوند انگلیس (GBP)",
                    CurrencyCode.AED => "درهم امارات (AED)",
                    CurrencyCode.TRY => "لیر ترکیه (TRY)",
                    _ => c
                },
                Selected = c == CurrencyCode.IRR
            }).ToList(),
            TaxMethodOptions = TaxCalculationMethod.All.Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = t,
                Text = t switch
                {
                    TaxCalculationMethod.Exclusive => "افزوده به مبلغ (Exclusive)",
                    TaxCalculationMethod.Inclusive => "شامل در مبلغ (Inclusive)",
                    TaxCalculationMethod.None => "بدون مالیات (None)",
                    _ => t
                },
                Selected = t == TaxCalculationMethod.Exclusive
            }).ToList(),
            TypeFields = typeFields
        };
    }

    public async Task<Contract> CreateContractAsync(ContractCreateVM model, string currentUserId, CancellationToken ct = default)
    {
        // Server-controlled field enforcement: a creator may only create in a Draft
        // (or the user-default) state — not jump straight to Approved/Active/Completed.
        // The acting user is the creator; assignment to another user requires elevation.
        var isAdmin = await IsAdminAsync(currentUserId);
        var requestedStatus = string.IsNullOrWhiteSpace(model.Status) ? ContractStatuses.Draft : model.Status;

        // New contracts may only start in Draft or PendingApproval (not active/approved/completed).
        if (requestedStatus != ContractStatuses.Draft && requestedStatus != ContractStatuses.PendingApproval)
        {
            if (!isAdmin)
                throw new UnauthorizedAccessException("وضعیت اولیه قرارداد باید «پیش‌نویس» یا «در انتظار تایید» باشد.");
        }

        // A non-admin cannot assign the contract to another user; assignment is server-controlled.
        if (!isAdmin && !string.IsNullOrWhiteSpace(model.AssignedUserId) && model.AssignedUserId != currentUserId)
        {
            throw new UnauthorizedAccessException("شما مجاز به واگذاری قرارداد به کاربر دیگر نیستید.");
        }

        // Auto-generate contract number if not provided
        if (string.IsNullOrWhiteSpace(model.ContractNumber))
        {
            model.ContractNumber = await GenerateNextContractNumberAsync(ct);
        }
        else
        {
            model.ContractNumber = model.ContractNumber.Trim();
        }

        // Validate unique contract number (fail fast for a user-supplied duplicate).
        if (!await IsContractNumberUniqueAsync(model.ContractNumber, null, ct))
            throw new InvalidOperationException("شماره قرارداد تکراری است.");

        var (totalTax, totalInclusive, baseCurrencyAmount) = CalculateTaxAndCurrency(
            model.TotalAmount, model.TaxMethod, model.DefaultTaxRatePercent, model.CurrencyCode, model.ExchangeRate);

        var contract = new Contract
        {
            ContractNumber = model.ContractNumber.Trim(),
            Title = model.Title.Trim(),
            ContractType = model.ContractType,
            ClientName = model.ClientName.Trim(),
            ClientPhone = model.ClientPhone?.Trim(),
            ClientAddress = model.ClientAddress?.Trim(),
            CustomerId = model.CustomerId,
            Description = model.Description?.Trim(),
            Notes = model.Notes?.Trim(),
            TermsAndConditions = OfficeAutomation.Utilities.HtmlSanitizer.Sanitize(model.TermsAndConditions),
            ContractBodyHtml = OfficeAutomation.Utilities.HtmlSanitizer.Sanitize(model.ContractBodyHtml),
            TotalAmount = model.TotalAmount,
            CurrencyCode = string.IsNullOrWhiteSpace(model.CurrencyCode) ? CurrencyCode.IRR : model.CurrencyCode,
            ExchangeRate = model.ExchangeRate > 0 ? model.ExchangeRate : 1m,
            TaxMethod = string.IsNullOrWhiteSpace(model.TaxMethod) ? TaxCalculationMethod.Exclusive : model.TaxMethod,
            DefaultTaxRatePercent = model.DefaultTaxRatePercent,
            TotalTaxAmount = totalTax,
            TotalAmountInclusiveTax = totalInclusive,
            BaseCurrencyAmount = baseCurrencyAmount,
            PaidAmount = 0,
            RemainingBalance = model.TotalAmount,
            PaymentStatus = PaymentStatuses.Unpaid,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            SignDate = model.SignDate,
            ExpiryNoticeDays = model.ExpiryNoticeDays > 0 ? model.ExpiryNoticeDays : 30,
            Status = requestedStatus,
            AssignedUserId = isAdmin ? model.AssignedUserId : currentUserId,
            TemplateId = model.TemplateId,
            CreatedByUserId = currentUserId,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsAutoRenew = model.IsAutoRenew,
            AutoRenewDaysBeforeExpiry = model.AutoRenewDaysBeforeExpiry > 0 ? model.AutoRenewDaysBeforeExpiry : 7,
            AutoRenewTermMonths = model.AutoRenewTermMonths > 0 ? model.AutoRenewTermMonths : 12
        };

        // Render contract body from template if template selected
        if (contract.TemplateId.HasValue)
        {
            var template = await _context.ContractTemplates.FirstOrDefaultAsync(t => t.Id == contract.TemplateId, ct);
            if (template?.HtmlContent != null)
            {
                var customer = contract.CustomerId.HasValue
                    ? await _context.Customers.FindAsync(new object[] { contract.CustomerId.Value }, ct)
                    : null;
                var assignedUser = !string.IsNullOrEmpty(contract.AssignedUserId)
                    ? await _userManager.FindByIdAsync(contract.AssignedUserId)
                    : null;
                var renderedHtml = _templateRenderer.RenderTemplate(template.HtmlContent, contract, customer, assignedUser);
                contract.ContractBodyHtml = OfficeAutomation.Utilities.HtmlSanitizer.Sanitize(renderedHtml);
            }
        }

        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync(ct);

        // Create initial version 1.0 snapshot
        await _versionService.CreateVersionSnapshotAsync(
            contract, currentUserId, "Created", "قرارداد ایجاد شد",
            isMajor: true, skipIfUnchanged: false, ct: ct);

        // Log creation
        await AddAuditLogAsync(contract.Id, currentUserId, "Created", null, contract.Status, "قرارداد ایجاد شد", ct);

        // Save dynamic field values (EAV)
        if (!string.IsNullOrWhiteSpace(contract.ContractType) && model.FieldValues?.Count > 0)
        {
            await SaveFieldValuesAsync(contract.Id, contract.ContractType, model.FieldValues, ct);
        }

        _logger.LogInformation("Contract {ContractNumber} created by {UserId}", contract.ContractNumber, currentUserId);
        return contract;
    }

    public async Task<ContractEditVM> BuildEditViewModelAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var c = await _context.Contracts.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.ContractTemplate)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Contract {id} not found.");

        var typeFields = await GetAllTypeFieldDefinitionsAsync(ct);
        var fieldValues = await LoadFieldValuesAsync(id, ct);

        return new ContractEditVM
        {
            Id = c.Id,
            ContractNumber = c.ContractNumber,
            Title = c.Title,
            ContractType = c.ContractType,
            ClientName = c.ClientName,
            ClientPhone = c.ClientPhone,
            ClientAddress = c.ClientAddress,
            CustomerId = c.CustomerId,
            Description = c.Description,
            Notes = c.Notes,
            TermsAndConditions = c.TermsAndConditions,
            ContractBodyHtml = c.ContractBodyHtml,
            TotalAmount = c.TotalAmount,
            CurrencyCode = string.IsNullOrWhiteSpace(c.CurrencyCode) ? CurrencyCode.IRR : c.CurrencyCode,
            ExchangeRate = c.ExchangeRate > 0 ? c.ExchangeRate : 1m,
            TaxMethod = string.IsNullOrWhiteSpace(c.TaxMethod) ? TaxCalculationMethod.Exclusive : c.TaxMethod,
            DefaultTaxRatePercent = c.DefaultTaxRatePercent,
            TotalTaxAmount = c.TotalTaxAmount,
            TotalAmountInclusiveTax = c.TotalAmountInclusiveTax,
            PaidAmount = c.PaidAmount,
            RemainingBalance = c.RemainingBalance,
            PaymentStatus = c.PaymentStatus,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            SignDate = c.SignDate,
            ExpiryNoticeDays = c.ExpiryNoticeDays,
            Status = c.Status,
            AssignedUserId = c.AssignedUserId,
            TemplateId = c.TemplateId,
            IsAutoRenew = c.IsAutoRenew,
            AutoRenewDaysBeforeExpiry = c.AutoRenewDaysBeforeExpiry,
            AutoRenewTermMonths = c.AutoRenewTermMonths,
            StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == c.Status }).ToList(),
            ContractTypeOptions = ContractTypes.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == c.ContractType }).ToList(),
            CurrencyOptions = CurrencyCode.All.Select(cur => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = cur,
                Text = cur switch
                {
                    CurrencyCode.IRR => "ریال ایران (IRR)",
                    CurrencyCode.USD => "دلار آمریکا (USD)",
                    CurrencyCode.EUR => "یورو (EUR)",
                    CurrencyCode.GBP => "پوند انگلیس (GBP)",
                    CurrencyCode.AED => "درهم امارات (AED)",
                    CurrencyCode.TRY => "لیر ترکیه (TRY)",
                    _ => cur
                },
                Selected = cur == c.CurrencyCode
            }).ToList(),
            TaxMethodOptions = TaxCalculationMethod.All.Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = t,
                Text = t switch
                {
                    TaxCalculationMethod.Exclusive => "افزوده به مبلغ (Exclusive)",
                    TaxCalculationMethod.Inclusive => "شامل در مبلغ (Inclusive)",
                    TaxCalculationMethod.None => "بدون مالیات (None)",
                    _ => t
                },
                Selected = t == c.TaxMethod
            }).ToList(),
            UserOptions = await GetUserSelectListAsync(c.AssignedUserId, ct),
            TemplateOptions = await GetTemplateSelectListAsync(currentUserId, c.TemplateId, ct),
            CustomerOptions = await GetCustomerSelectListAsync(c.CustomerId, ct),
            TypeFields = typeFields,
            FieldValues = fieldValues.ToDictionary(f => f.FieldKey, f => f.Value ?? string.Empty)
        };
    }

    public async Task<Contract> UpdateContractAsync(ContractEditVM model, string currentUserId, CancellationToken ct = default)
    {
        // Validate unique contract number (excluding current)
        if (!await IsContractNumberUniqueAsync(model.ContractNumber, model.Id, ct))
            throw new InvalidOperationException("شماره قرارداد تکراری است.");

        var c = await _context.Contracts
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == model.Id, ct)
            ?? throw new KeyNotFoundException($"Contract {model.Id} not found.");

        var isAdmin = await IsAdminAsync(currentUserId);
        var oldStatus = c.Status;
        var requestedStatus = string.IsNullOrWhiteSpace(model.Status) ? c.Status : model.Status;

        // Validate the status transition via the state machine.
        await ValidateContractStateTransition(oldStatus, requestedStatus, isAdmin, currentUserId, c.Id);

        // Enforce document requirements: Draft→PendingApproval requires all mandatory docs uploaded
        if (oldStatus == ContractStatuses.Draft && requestedStatus == ContractStatuses.PendingApproval)
        {
            await EnforceDocumentRequirementsAsync(c.Id);
        }

        // A non-admin cannot reassign the contract to a different user (server-controlled).
        if (!isAdmin && !string.IsNullOrWhiteSpace(model.AssignedUserId) && model.AssignedUserId != currentUserId
            && model.AssignedUserId != c.AssignedUserId)
        {
            throw new UnauthorizedAccessException("شما مجاز به واگذاری قرارداد به کاربر دیگر نیستید.");
        }

        c.ContractNumber = model.ContractNumber.Trim();
        c.Title = model.Title.Trim();
        c.ContractType = model.ContractType;
        c.ClientName = model.ClientName.Trim();
        c.ClientPhone = model.ClientPhone?.Trim();
        c.ClientAddress = model.ClientAddress?.Trim();
        c.CustomerId = model.CustomerId;
        c.Description = model.Description?.Trim();
        c.Notes = model.Notes?.Trim();
        c.TermsAndConditions = OfficeAutomation.Utilities.HtmlSanitizer.Sanitize(model.TermsAndConditions);
        c.ContractBodyHtml = OfficeAutomation.Utilities.HtmlSanitizer.Sanitize(model.ContractBodyHtml);
        c.TotalAmount = model.TotalAmount;

        var (totalTax, totalInclusive, baseCurrencyAmount) = CalculateTaxAndCurrency(
            model.TotalAmount, model.TaxMethod, model.DefaultTaxRatePercent, model.CurrencyCode, model.ExchangeRate);

        c.CurrencyCode = string.IsNullOrWhiteSpace(model.CurrencyCode) ? CurrencyCode.IRR : model.CurrencyCode;
        c.ExchangeRate = model.ExchangeRate > 0 ? model.ExchangeRate : 1m;
        c.TaxMethod = string.IsNullOrWhiteSpace(model.TaxMethod) ? TaxCalculationMethod.Exclusive : model.TaxMethod;
        c.DefaultTaxRatePercent = model.DefaultTaxRatePercent;
        c.TotalTaxAmount = totalTax;
        c.TotalAmountInclusiveTax = totalInclusive;
        c.BaseCurrencyAmount = baseCurrencyAmount;

        c.StartDate = model.StartDate;
        c.EndDate = model.EndDate;
        c.SignDate = model.SignDate;
        c.ExpiryNoticeDays = model.ExpiryNoticeDays > 0 ? model.ExpiryNoticeDays : 30;
        c.Status = requestedStatus;
        c.AssignedUserId = isAdmin ? model.AssignedUserId : (model.AssignedUserId ?? c.AssignedUserId);
        c.TemplateId = model.TemplateId;
        c.UpdatedAt = DateTime.UtcNow;
        c.IsAutoRenew = model.IsAutoRenew;
        c.AutoRenewDaysBeforeExpiry = model.AutoRenewDaysBeforeExpiry > 0 ? model.AutoRenewDaysBeforeExpiry : 7;
        c.AutoRenewTermMonths = model.AutoRenewTermMonths > 0 ? model.AutoRenewTermMonths : 12;

        // Recalculate payment status
        c.PaidAmount = c.Payments.Sum(p => p.Amount);
        c.RemainingBalance = c.TotalAmount - c.PaidAmount;
        if (c.RemainingBalance <= 0 && c.TotalAmount > 0)
            c.PaymentStatus = PaymentStatuses.FullyPaid;
        else if (c.PaidAmount > 0)
            c.PaymentStatus = PaymentStatuses.PartiallyPaid;
        else
            c.PaymentStatus = PaymentStatuses.Unpaid;

        await _context.SaveChangesAsync(ct);

        // ── Log audit FIRST so history is never lost if version snapshot fails ──
        if (oldStatus != model.Status)
        {
            await AddAuditLogAsync(c.Id, currentUserId, "StatusChanged", oldStatus, model.Status, $"تغییر وضعیت از {oldStatus} به {model.Status}", ct);
        }
        else
        {
            await AddAuditLogAsync(c.Id, currentUserId, "Edited", null, null, "قرارداد ویرایش شد", ct);
        }

        // Create version snapshot after update (non-critical — catch errors)
        try
        {
            var isStatusChange = oldStatus != model.Status;
            var changeDescription = isStatusChange
                ? $"تغییر وضعیت از {oldStatus} به {model.Status}"
                : "قرارداد ویرایش شد";
            await _versionService.CreateVersionSnapshotAsync(
                c, currentUserId, isStatusChange ? "StatusChange" : "Edited", changeDescription,
                isMajor: false, skipIfUnchanged: true, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Version snapshot creation failed for contract {ContractId} — audit log already saved.", c.Id);
        }

        // ── Accrual accounting: book deferred revenue when contract becomes Active ──
        if (oldStatus != ContractStatuses.Active && model.Status == ContractStatuses.Active)
        {
            await _contractAccounting.RecordDeferredRevenueAsync(c, currentUserId, ct);
        }

        // Save dynamic field values (EAV)
        if (!string.IsNullOrWhiteSpace(c.ContractType) && model.FieldValues?.Count > 0)
        {
            await SaveFieldValuesAsync(c.Id, c.ContractType, model.FieldValues, ct);
        }

        _logger.LogInformation("Contract {ContractNumber} updated by {UserId}", c.ContractNumber, currentUserId);
        return c;
    }

    public async Task<bool> DeleteContractAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var c = await _context.Contracts
            .Include(x => x.Payments)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return false;

        // Delete related penalty rules first (FK constraint FK_PaymentPenaltyRules_Contracts_ContractId)
        var penaltyRules = await _context.PaymentPenaltyRules
            .Where(r => r.ContractId == id)
            .ToListAsync(ct);
        if (penaltyRules.Count > 0)
        {
            _context.PaymentPenaltyRules.RemoveRange(penaltyRules);
        }

        // Delete the per-contract upload directory so attachment files do not remain
        // orphaned on disk (residual sensitive data / storage leak).
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts", id.ToString());
        try
        {
            if (Directory.Exists(uploadsDir))
                Directory.Delete(uploadsDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            _logger.LogWarning(ex, "Failed to delete upload directory for contract {ContractId}", id);
        }

        _context.Contracts.Remove(c);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Contract {ContractNumber} deleted by {UserId}", c.ContractNumber, currentUserId);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Archive
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractArchiveVM> GetArchiveViewModelAsync(ContractArchiveVM filter, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        var query = _context.Contracts.AsNoTracking()
            .Include(c => c.AssignedUser)
            .Include(c => c.ArchivedByUser)
            .Include(c => c.Customer)
            .Where(c => c.IsArchived)
            .AsQueryable();

        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(c => c.Title.Contains(term) || c.ContractNumber.Contains(term) || c.ClientName.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(c => c.Status == filter.Status);
        }
        if (!string.IsNullOrWhiteSpace(filter.ContractType))
        {
            query = query.Where(c => c.ContractType == filter.ContractType);
        }
        if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
        {
            query = query.Where(c => c.PaymentStatus == filter.PaymentStatus);
        }
        if (!string.IsNullOrWhiteSpace(filter.AssignedUserId))
        {
            query = query.Where(c => c.AssignedUserId == filter.AssignedUserId);
        }
        if (filter.StartDateFrom.HasValue)
        {
            query = query.Where(c => c.StartDate >= filter.StartDateFrom.Value);
        }
        if (filter.StartDateTo.HasValue)
        {
            query = query.Where(c => c.StartDate <= filter.StartDateTo.Value);
        }

        filter.Items = await query.OrderByDescending(c => c.ArchivedAt)
            .Select(c => new ContractIndexItemVM
            {
                Id = c.Id,
                ContractNumber = c.ContractNumber,
                Title = c.Title,
                ClientName = c.ClientName,
                CustomerName = c.Customer != null ? c.Customer.Name : null,
                ContractType = c.ContractType,
                TotalAmount = c.TotalAmount,
                PaidAmount = c.PaidAmount,
                RemainingBalance = c.RemainingBalance,
                PaymentStatus = c.PaymentStatus,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                AssignedUserId = c.AssignedUserId,
                ResponsibleUserName = c.AssignedUser != null ? c.AssignedUser.FullName : null,
                IsArchived = true,
                IsOwnedByCurrentUser = c.AssignedUserId == currentUserId || c.CreatedByUserId == currentUserId,
                CreatedAt = c.CreatedAt,
                ArchivedAt = c.ArchivedAt,
                ArchivedByUserName = c.ArchivedByUser != null ? c.ArchivedByUser.FullName : null
            }).ToListAsync(ct);
        filter.TotalCount = await _context.Contracts.AsNoTracking().CountAsync(c => c.IsArchived, ct);
        filter.FilteredCount = filter.Items.Count;
        filter.StatusOptions = ContractStatuses.GetStatusOptions();
        filter.ContractTypeOptions = ContractTypes.GetTypeOptions();
        filter.PaymentStatusOptions = PaymentStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s }).ToList();
        filter.ResponsibleUserOptions = await _userManager.Users.OrderBy(u => u.FullName)
            .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u.Id, Text = u.FullName ?? u.UserName ?? u.Email ?? u.Id }).ToListAsync(ct);
        return filter;
    }

    public async Task<bool> ArchiveContractAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var c = await _context.Contracts.FindAsync(new object[] { id }, ct);
        if (c == null) return false;
        var oldStatus = c.Status;
        c.IsArchived = true; c.ArchivedAt = DateTime.Now; c.ArchivedByUserId = currentUserId;
        c.Status = ContractStatuses.Archived; c.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
        await AddAuditLogAsync(c.Id, currentUserId, "Archived", oldStatus, ContractStatuses.Archived, "قرارداد بایگانی شد", ct);
        _logger.LogInformation("Contract {ContractNumber} archived by {UserId}", c.ContractNumber, currentUserId);
        return true;
    }

    public async Task<bool> UnarchiveContractAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var c = await _context.Contracts.FindAsync(new object[] { id }, ct);
        if (c == null) return false;

        // Restore the previous valid status (from the last audit-log entry before
        // archiving) instead of blindly forcing "Active". Defaults to Active only if
        // no prior state can be determined.
        var previousStatus = await _context.ContractAuditLogs.AsNoTracking()
            .Where(al => al.ContractId == id && al.NewStatus != ContractStatuses.Archived && al.NewStatus != null)
            .OrderByDescending(al => al.CreatedAt)
            .Select(al => al.NewStatus)
            .FirstOrDefaultAsync(ct);

        var restoredStatus = string.IsNullOrWhiteSpace(previousStatus)
            ? ContractStatuses.Active
            : previousStatus;

        c.IsArchived = false; c.ArchivedAt = null; c.ArchivedByUserId = null;
        c.Status = restoredStatus; c.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await AddAuditLogAsync(c.Id, currentUserId, "Unarchived", ContractStatuses.Archived, restoredStatus, "قرارداد از بایگانی خارج شد", ct);
        _logger.LogInformation("Contract {ContractNumber} unarchived by {UserId} to status {Status}", c.ContractNumber, currentUserId, restoredStatus);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Payments
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractPayment> AddPaymentAsync(ContractPaymentCreateVM model, string currentUserId, CancellationToken ct = default)
    {
        // Object-level authorization: the caller must have access to the parent contract.
        await EnforceAccessAsync(model.ContractId, currentUserId, ct);

        // Persist the payment and recompute financials inside a single transaction so
        // concurrent payment creation cannot produce inconsistent totals (lost update).
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var oldTotal = await _context.Contracts.AsNoTracking()
            .Where(x => x.Id == model.ContractId)
            .Select(x => x.TotalAmount)
            .FirstOrDefaultAsync(ct);

        var payment = new ContractPayment
        {
            ContractId = model.ContractId,
            Amount = model.Amount,
            PaymentDate = model.PaymentDate,
            DueDate = model.DueDate,
            Status = model.Status,
            PaymentMethod = model.PaymentMethod?.Trim(),
            ReferenceNumber = model.ReferenceNumber?.Trim(),
            Notes = model.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.ContractPayments.Add(payment);

        // Server-side aggregate: recompute from the actual payment rows rather than
        // relying on a stale in-memory Payments collection.
        var totalPaid = await _context.ContractPayments
            .Where(p => p.ContractId == model.ContractId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var contract = await _context.Contracts.FindAsync(new object[] { model.ContractId }, ct)
            ?? throw new KeyNotFoundException($"Contract {model.ContractId} not found.");

        contract.PaidAmount = totalPaid;
        contract.RemainingBalance = contract.TotalAmount - contract.PaidAmount;
        contract.PaymentStatus = contract.RemainingBalance <= 0 && contract.TotalAmount > 0
            ? PaymentStatuses.FullyPaid
            : (contract.PaidAmount > 0 ? PaymentStatuses.PartiallyPaid : PaymentStatuses.Unpaid);
        contract.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

// ── Accounting integration: create accrual-basis journal entry ──
        if (model.Status == "Paid")
        {
            await _contractAccounting.RecordCashReceiptAsync(contract, payment, currentUserId, ct);
        }

        // Payment audit detail: capture actor, payment ID, contract ID, operation,
        // Payment audit detail: capture actor, payment ID, contract ID, operation,
        // timestamp, and the previous/new PaidAmount for traceability.
        await AddAuditLogAsync(
            model.ContractId,
            currentUserId,
            "PaymentAdded",
            null,
            null,
            $"پرداخت به مبلغ {model.Amount:N0} ثبت شد. شناسه پرداخت: {payment.Id}. مبلغ پرداخت‌شده قبلی: {oldTotal - model.Amount:N0} → جدید: {totalPaid:N0}",
            ct);

        _logger.LogInformation("Payment {Amount} added to contract {ContractId} by {UserId}", model.Amount, model.ContractId, currentUserId);

        // Auto-create default penalty rule if none exists
        try { await _penaltyService.CreateDefaultRuleAsync(model.ContractId, ct); } catch { /* non-critical */ }

        return payment;
    }

    public async Task<bool> DeletePaymentAsync(int paymentId, int contractId, string currentUserId, CancellationToken ct = default)
    {
        // Object-level authorization: the caller must have access to the parent contract.
        await EnforceAccessAsync(contractId, currentUserId, ct);

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var payment = await _context.ContractPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.ContractId == contractId, ct);
        if (payment == null)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        _context.ContractPayments.Remove(payment);

        // Server-side aggregate recompute.
        var totalPaid = await _context.ContractPayments
            .Where(p => p.ContractId == contractId && p.Id != paymentId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var contract = await _context.Contracts.FindAsync(new object[] { contractId }, ct);
        if (contract != null)
        {
            contract.PaidAmount = totalPaid;
            contract.RemainingBalance = contract.TotalAmount - contract.PaidAmount;
            contract.PaymentStatus = contract.RemainingBalance <= 0 && contract.TotalAmount > 0
                ? PaymentStatuses.FullyPaid
                : (contract.PaidAmount > 0 ? PaymentStatuses.PartiallyPaid : PaymentStatuses.Unpaid);
            contract.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

// ── Accounting integration: reverse the payment journal entry ──
        if (payment.Status == "Paid" && contract != null)
        {
            await _contractAccounting.RevertCashReceiptAsync(contract, payment, currentUserId, ct);
        }

        // Payment audit detail: capture actor, payment ID, contract ID, operation,
        // Payment audit detail: capture actor, payment ID, contract ID, operation,
        // timestamp, and the previous/new PaidAmount for traceability.
        await AddAuditLogAsync(
            contractId,
            currentUserId,
            "PaymentDeleted",
            null,
            null,
            $"پرداخت با شناسه {paymentId} به مبلغ {payment.Amount:N0} حذف شد. مبلغ پرداخت‌شده قبلی: {totalPaid + payment.Amount:N0} → جدید: {totalPaid:N0}",
            ct);

        _logger.LogInformation("Payment {PaymentId} deleted from contract {ContractId} by {UserId}", paymentId, contractId, currentUserId);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Attachments
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Restricts uploads to a safe set of file types (by extension, MIME and magic bytes)
    /// and imposes a maximum size. Returns false with a reason when the upload is rejected.
    /// </summary>
    private static readonly string[] AllowedAttachmentExtensions =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".txt", ".zip"];

    private static readonly string[] AllowedAttachmentMimeTypes =
        ["application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
         "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
         "image/jpeg", "image/png", "image/gif", "image/webp", "text/plain", "application/zip", "application/octet-stream"];

    private const long MaxAttachmentSizeBytes = 20 * 1024 * 1024; // 20 MB

    public ContractAttachment? ValidateAttachmentUpload(string fileName, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        if (sizeBytes <= 0 || sizeBytes > MaxAttachmentSizeBytes) return null;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedAttachmentExtensions.Contains(extension)) return null;

        if (!string.IsNullOrWhiteSpace(contentType) &&
            !AllowedAttachmentMimeTypes.Contains(contentType.ToLowerInvariant()))
        {
            return null;
        }

        return new ContractAttachment();
    }

    /// <summary>
    /// Determines whether the extension is considered executable / dangerous.
    /// </summary>
    private static bool IsDangerousExtension(string extension)
    {
        string[] dangerous = [".exe", ".bat", ".cmd", ".com", ".sh", ".ps1", ".msi", ".dll", ".scr", ".vbs", ".js", ".jsp", ".php", ".asp", ".aspx", ".html", ".htm", ".svg"];
        return dangerous.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ContractAttachment> UploadAttachmentAsync(int contractId, string fileName, Stream fileStream, string contentType, string uploadedByUserId, CancellationToken ct = default)
    {
        var contract = await _context.Contracts.FindAsync(new object[] { contractId }, ct)
            ?? throw new KeyNotFoundException($"Contract {contractId} not found.");

        // Validate the upload (extension + MIME + signature + size) before writing.
        var extension = Path.GetExtension(fileName);
        if (IsDangerousExtension(extension) || SniffContentTypeByExtension(extension) == null)
        {
            throw new InvalidOperationException("نوع فایل مجاز نیست.");
        }

        // Read magic bytes to confirm the file actually matches its declared type.
        var signature = await ReadHeaderBytesAsync(fileStream, 8, ct);
        if (!IsSignatureValid(extension, signature))
        {
            throw new InvalidOperationException("محتوای فایل با نوع اعلام‌شده مطابقت ندارد.");
        }

        if (fileStream.Length > MaxAttachmentSizeBytes)
            throw new InvalidOperationException("حجم فایل بیش از حد مجاز است.");

        // Reset stream position so the full content is written below.
        fileStream.Position = 0;

        // Server-generated file name (never trust the client filename for the physical path).
        var safeName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts", contractId.ToString());
        Directory.CreateDirectory(uploadsDir);

        // Resolve and verify the canonical path stays within the uploads root.
        string fullPath;
        try
        {
            var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts"));
            var candidate = Path.GetFullPath(Path.Combine(uploadsDir, safeName));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("مسیر ذخیره‌سازی نامعتبر است.");
            fullPath = candidate;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException("مسیر ذخیره‌سازی نامعتبر است.");
        }

        // Read the stream for magic-byte validation then write with a size cap.
        await using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            // Copy with a size cap to prevent unbounded writes.
            await CopyWithLimitAsync(fileStream, fs, MaxAttachmentSizeBytes, ct);
        }

        var attachment = new ContractAttachment
        {
            ContractId = contractId,
            FileName = Path.GetFileName(fileName), // strip any path from the client name
            StoredFileName = safeName,
            RelativePath = $"/uploads/contracts/{contractId}/{safeName}",
            ContentType = contentType,
            FileSize = new FileInfo(fullPath).Length,
            UploadedByUserId = uploadedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ContractAttachments.Add(attachment);
        await _context.SaveChangesAsync(ct);

        // Create initial version record so CurrentVersionId is set for preview & version tracking
        try
        {
            var versionDir = Path.Combine(uploadsDir, "versions");
            Directory.CreateDirectory(versionDir);
            var versionFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var versionPath = Path.Combine(versionDir, versionFileName);
            System.IO.File.Copy(fullPath, versionPath, overwrite: false);

            var version = new ContractAttachmentVersion
            {
                AttachmentId = attachment.Id,
                VersionNumber = 1,
                FileName = attachment.FileName,
                StoredFileName = versionFileName,
                RelativePath = Path.Combine("uploads", "contracts", contractId.ToString(), "versions", versionFileName),
                ContentType = attachment.ContentType,
                FileSize = attachment.FileSize,
                ChangeDescription = "نسخه اولیه",
                UploadedByUserId = uploadedByUserId
            };
            _context.ContractAttachmentVersions.Add(version);
            await _context.SaveChangesAsync(ct);

            attachment.CurrentVersionId = version.Id;
            attachment.VersionNumber = 1;
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Non-critical: version record creation failed, fallback to RelativePath preview
            _logger.LogWarning(ex, "Failed to create initial version for attachment {AttachmentId}, falling back to RelativePath preview", attachment.Id);
        }

        _logger.LogInformation("Attachment {FileName} uploaded to contract {ContractId}", attachment.FileName, contractId);
        return attachment;
    }

    private async Task<byte[]> ReadHeaderBytesAsync(Stream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, count - total), ct);
            if (read == 0) break;
            total += read;
        }
        return buffer[..total];
    }

    private static string? SniffContentTypeByExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            _ => null
        };
    }

    /// <summary>
    /// Validates that the leading magic bytes of a file match the declared extension.
    /// </summary>
    private static bool IsSignatureValid(string extension, byte[] signature)
    {
        if (signature.Length == 0) return false;

        static bool Match(byte[] data, ReadOnlySpan<byte> magic)
        {
            if (data.Length < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
                if (data[i] != magic[i]) return false;
            return true;
        }

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => Match(signature, new byte[] { 0x25, 0x50, 0x44, 0x46 }),
            ".png" => Match(signature, new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            ".jpg" or ".jpeg" => Match(signature, new byte[] { 0xFF, 0xD8, 0xFF }),
            ".gif" => Match(signature, new byte[] { 0x47, 0x49, 0x46, 0x38 }),
            ".zip" => Match(signature, new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
            // Office/Text files: signature validation is best-effort.
            ".doc" or ".docx" or ".xls" or ".xlsx" or ".webp" or ".txt" => true,
            _ => false
        };
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, long limit, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > limit)
                throw new InvalidOperationException("حجم فایل بیش از حد مجاز است.");
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    public async Task<ContractAttachment?> GetAttachmentAsync(int attachmentId, string currentUserId, CancellationToken ct = default)
    {
        // Object-level authorization: verify the caller can access the parent contract.
        var attachment = await _context.ContractAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment == null) return null;
        await EnforceAccessAsync(attachment.ContractId, currentUserId, ct);
        return attachment;
    }

    /// <summary>
    /// Resolves the physical path for an attachment after verifying the caller has
    /// access to the parent contract, guarding against path traversal. Returns the
    /// attachment record and a validated full path, or (null, null) when unauthorized/missing.
    /// </summary>
    public async Task<(ContractAttachment? Attachment, string? FullPath)> GetAttachmentForDownloadAsync(int attachmentId, string currentUserId, CancellationToken ct = default)
    {
        var attachment = await _context.ContractAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment == null) return (null, null);

        await EnforceAccessAsync(attachment.ContractId, currentUserId, ct);

        var relativePath = attachment.RelativePath ?? string.Empty;
        // Reject any path that escapes the uploads root.
        if (relativePath.Contains("..") || relativePath.Contains(':') || relativePath.StartsWith("\\\\"))
            return (null, null);

        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts"));
        var candidate = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/')));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return (null, null);

        return (attachment, candidate);
    }

    public async Task<bool> DeleteAttachmentAsync(int attachmentId, int contractId, string currentUserId, CancellationToken ct = default)
    {
        // Object-level authorization.
        await EnforceAccessAsync(contractId, currentUserId, ct);

        var attachment = await _context.ContractAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ContractId == contractId, ct);
        if (attachment == null) return false;

        // Try to delete the physical file — ignore errors (locked / missing / permission)
        try
        {
            var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts"));
            var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.RelativePath.TrimStart('/')));
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete physical file for attachment {AttachmentId} — removing DB record anyway.", attachmentId);
        }

        // Try to clean up version files
        if (attachment.CurrentVersionId.HasValue)
        {
            try
            {
                var versionsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts", contractId.ToString(), "versions");
                if (Directory.Exists(versionsDir))
                {
                    foreach (var versionFile in Directory.GetFiles(versionsDir))
                    {
                        try { File.Delete(versionFile); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up version files for attachment {AttachmentId}", attachmentId);
            }
        }

        _context.ContractAttachments.Remove(attachment);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Deletes an attachment by Id only (resolves contract from attachment record).
    /// Used as fallback when contractId parameter mismatches.
    /// </summary>
    public async Task<bool> DeleteAttachmentByIdOnlyAsync(int attachmentId, string currentUserId, CancellationToken ct = default)
    {
        var attachment = await _context.ContractAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment == null) return false;

        await EnforceAccessAsync(attachment.ContractId, currentUserId, ct);

        // Try to delete the physical file — ignore errors (locked / missing / permission)
        try
        {
            var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts"));
            var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.RelativePath.TrimStart('/')));
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete physical file for attachment {AttachmentId} (by-id-only) — removing DB record anyway.", attachmentId);
        }

        _context.ContractAttachments.Remove(attachment);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Attachment {AttachmentId} ({FileName}) deleted by {UserId}", attachmentId, attachment.FileName, currentUserId);
        return true;
    }

    public async Task<int> CleanupOrphanedAttachmentsAsync(int? contractId, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        if (!isAdmin) throw new UnauthorizedAccessException("Only admins can cleanup orphaned attachments.");

        var query = _context.ContractAttachments.AsNoTracking();
        if (contractId.HasValue)
            query = query.Where(a => a.ContractId == contractId.Value);

        var attachments = await query.ToListAsync(ct);
        var removedCount = 0;
        foreach (var attachment in attachments)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.RelativePath.TrimStart('/')));
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Removing orphaned attachment {AttachmentId} ({FileName}) - file not found at {Path}", attachment.Id, attachment.FileName, fullPath);
                var attachToRemove = await _context.ContractAttachments.FindAsync(new object[] { attachment.Id }, ct);
                if (attachToRemove != null)
                {
                    _context.ContractAttachments.Remove(attachToRemove);
                    removedCount++;
                }
            }
        }
        if (removedCount > 0)
            await _context.SaveChangesAsync(ct);
        return removedCount;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Templates / Settings
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractSettingsVM> GetSettingsViewModelAsync(string currentUserId, CancellationToken ct = default)
    {
        var templates = await _context.ContractTemplates.AsNoTracking()
            .Where(t => t.OwnerUserId == null || t.OwnerUserId == currentUserId)
            .OrderByDescending(t => t.IsDefault).ThenByDescending(t => t.UpdatedAt).ToListAsync(ct);
        var def = templates.FirstOrDefault(t => t.IsDefault);
        var setting = await _context.ContractUserSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == currentUserId, ct);
        var users = await _userManager.Users.OrderBy(u => u.FullName)
            .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u.Id, Text = u.FullName ?? u.UserName ?? u.Email ?? u.Id }).ToListAsync(ct);
        var availableTags = _templateRenderer.GetAvailableTags();
        return new ContractSettingsVM
        {
            TemplateName = def?.Name ?? string.Empty,
            HtmlContent = def?.HtmlContent,
            Description = def?.Description,
            IsDefault = def?.IsDefault ?? false,
            CurrentTemplateId = def?.Id,
            DefaultStatus = setting?.DefaultStatus ?? ContractStatuses.Draft,
            DefaultResponsibleUserId = setting?.DefaultResponsibleUserId,
            NotifyOnCreate = setting?.NotifyOnCreate ?? false,
            StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s }).ToList(),
            UserOptions = users,
            Templates = templates.Select(t => new ContractTemplateListItemVM
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                IsDefault = t.IsDefault,
                TemplateType = t.OwnerUserId == null ? "Global" : "Personal",
                UpdatedAt = t.UpdatedAt
            }).ToList(),
            AvailableTags = availableTags.Select(t => new ContractTemplateTagVM
            {
                Tag = t.Tag,
                DisplayName = t.DisplayName,
                Description = t.Description
            }).ToList()
        };
    }

    public async Task<ContractTemplate> SaveTemplateAsync(ContractSettingsVM model, string currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.TemplateName)) throw new ArgumentException("Template name is required.");
        ContractTemplate template;
        if (model.CurrentTemplateId.HasValue)
        {
            template = await _context.ContractTemplates.FirstOrDefaultAsync(t => t.Id == model.CurrentTemplateId && (t.OwnerUserId == null || t.OwnerUserId == currentUserId), ct)
                ?? throw new KeyNotFoundException("Template not found.");
        }
        else
        {
            template = new ContractTemplate { OwnerUserId = currentUserId, CreatedAt = DateTime.Now };
            _context.ContractTemplates.Add(template);
        }
        template.Name = model.TemplateName.Trim();
        template.HtmlContent = OfficeAutomation.Utilities.HtmlSanitizer.Sanitize(model.HtmlContent);
        template.Description = model.Description?.Trim();
        template.TemplateType = "Personal";
        template.UpdatedAt = DateTime.Now;

        if (model.IsDefault)
        {
            var defaults = await _context.ContractTemplates
                .Where(t => t.OwnerUserId == currentUserId && t.IsDefault && t.Id != template.Id).ToListAsync(ct);
            foreach (var d in defaults) d.IsDefault = false;
        }
        template.IsDefault = model.IsDefault;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Template {Name} saved for {UserId}", template.Name, currentUserId);
        return template;
    }

    public async Task<ContractUserSetting> SaveSettingsAsync(ContractSettingsVM model, string currentUserId, CancellationToken ct = default)
    {
        var setting = await _context.ContractUserSettings.FirstOrDefaultAsync(s => s.UserId == currentUserId, ct);
        if (setting == null)
        {
            setting = new ContractUserSetting { UserId = currentUserId };
            _context.ContractUserSettings.Add(setting);
        }
        setting.DefaultStatus = string.IsNullOrWhiteSpace(model.DefaultStatus) ? ContractStatuses.Draft : model.DefaultStatus;
        setting.DefaultResponsibleUserId = model.DefaultResponsibleUserId;
        setting.NotifyOnCreate = model.NotifyOnCreate;
        setting.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Settings saved for {UserId}", currentUserId);
        return setting;
    }

    public async Task<bool> DeleteTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default)
    {
        var t = await _context.ContractTemplates.FirstOrDefaultAsync(x => x.Id == templateId && (x.OwnerUserId == null || x.OwnerUserId == currentUserId), ct);
        if (t == null) return false;
        var contracts = await _context.Contracts.Where(c => c.TemplateId == templateId).ToListAsync(ct);
        foreach (var c in contracts) c.TemplateId = null;
        _context.ContractTemplates.Remove(t);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetDefaultTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default)
    {
        var t = await _context.ContractTemplates.FirstOrDefaultAsync(x => x.Id == templateId && (x.OwnerUserId == null || x.OwnerUserId == currentUserId), ct);
        if (t == null) return false;
        var defaults = await _context.ContractTemplates.Where(x => (x.OwnerUserId == null || x.OwnerUserId == currentUserId) && x.IsDefault && x.Id != templateId).ToListAsync(ct);
        foreach (var d in defaults) d.IsDefault = false;
        t.IsDefault = true; t.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<ContractTemplate>> GetUserTemplatesAsync(string currentUserId, CancellationToken ct = default)
    {
        return await _context.ContractTemplates.AsNoTracking()
            .Where(t => t.OwnerUserId == null || t.OwnerUserId == currentUserId)
            .OrderByDescending(t => t.IsDefault).ThenByDescending(t => t.UpdatedAt).ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Document Generation & Template Rendering
    // ═══════════════════════════════════════════════════════════════

    public async Task<string> GenerateContractDocumentAsync(int contractId, int? templateId, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        IQueryable<Contract> query = _context.Contracts.AsNoTracking()
            .Include(c => c.Customer)
            .Include(c => c.AssignedUser)
            .Include(c => c.ContractTemplate);

        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);

        var contract = await query.FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new KeyNotFoundException($"Contract {contractId} not found.");

        // Get template HTML
        ContractTemplate? template = null;
        if (templateId.HasValue)
            template = await _context.ContractTemplates.FirstOrDefaultAsync(t => t.Id == templateId, ct);
        else
            template = await _context.ContractTemplates
                .Where(t => (t.OwnerUserId == null || t.OwnerUserId == currentUserId) && t.IsDefault)
                .FirstOrDefaultAsync(ct);

        var html = template?.HtmlContent ?? GetDefaultTemplateHtml();

        // Use template rendering service
        var customer = contract.CustomerId.HasValue
            ? await _context.Customers.FindAsync(new object[] { contract.CustomerId.Value }, ct)
            : null;
        var assignedUser = !string.IsNullOrEmpty(contract.AssignedUserId)
            ? await _userManager.FindByIdAsync(contract.AssignedUserId)
            : null;

        var renderedBody = _templateRenderer.RenderTemplate(html, contract, customer, assignedUser);

        // Get branding settings
        var branding = await _tenantSettings.GetSystemSettingsAsync(ct);
        var companyName = branding.ApplicationTitle ?? "شرکت راینو";
        var logoUrl = branding.LogoUrl;
        var primaryColor = branding.PrimaryColor ?? "#0d9488";

        // Wrap rendered body in branded print layout with signature block
        return WrapWithBrandedPrintLayout(renderedBody, companyName, logoUrl, primaryColor, contract);
    }

    private static string WrapWithBrandedPrintLayout(string bodyHtml, string companyName, string? logoUrl, string primaryColor, Contract contract)
    {
        var logoHtml = !string.IsNullOrWhiteSpace(logoUrl)
            ? $"<img src=\"{logoUrl}\" alt=\"{companyName}\" style=\"height:48px;width:auto;\" />"
            : $"<strong style=\"font-size:18px;color:{primaryColor};\">{companyName}</strong>";

        return $@"<!DOCTYPE html>
<html dir='rtl' lang='fa'>
<head>
    <meta charset='utf-8' />
    <title>قرارداد - {contract.ContractNumber}</title>
    <style>
        @page {{ margin: 20mm 15mm; }}
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: Vazirmatn, Tahoma, sans-serif;
            font-size: 12px;
            line-height: 1.8;
            color: #1e293b;
            background: #fff;
            padding: 30px;
        }}
        .print-header {{
            text-align: center;
            padding-bottom: 20px;
            margin-bottom: 25px;
            border-bottom: 2px solid {primaryColor};
        }}
        .print-header .logo {{ margin-bottom: 8px; }}
        .print-header h1 {{ font-size: 18px; color: {primaryColor}; margin-bottom: 4px; }}
        .print-header .meta {{ font-size: 11px; color: #64748b; }}
        .print-body {{ padding: 10px 0; }}
        .print-body table {{ width: 100%; border-collapse: collapse; margin: 10px 0; }}
        .print-body table th, .print-body table td {{
            border: 1px solid #e2e8f0;
            padding: 6px 10px;
            text-align: right;
            font-size: 11px;
        }}
        .print-body table th {{ background: #f8fafc; font-weight: 700; }}
        .print-footer {{
            margin-top: 50px;
            padding-top: 20px;
            border-top: 1px solid #e2e8f0;
        }}
        .signature-section {{
            display: flex;
            justify-content: space-between;
            gap: 30px;
            margin-top: 30px;
            page-break-inside: avoid;
            break-inside: avoid;
        }}
        .signature-box {{
            flex: 1;
            border: 1.5px solid #94a3b8;
            border-radius: 6px;
            padding: 20px 15px;
            min-height: 140px;
            text-align: center;
        }}
        .signature-box h4 {{
            font-size: 12px;
            font-weight: 800;
            color: {primaryColor};
            margin-bottom: 8px;
        }}
        .signature-box .line {{
            border-bottom: 1px solid #cbd5e1;
            margin: 30px 0 6px;
        }}
        .signature-box .label {{
            font-size: 10px;
            color: #94a3b8;
        }}
        .watermark {{
            position: fixed;
            bottom: 10px;
            left: 10px;
            font-size: 9px;
            color: #cbd5e1;
        }}
        @@media print {{
            body {{ padding: 0; }}
            .signature-section {{ break-inside: avoid; page-break-inside: avoid; }}
        }}
    </style>
</head>
<body>
    <div class='print-header'>
        <div class='logo'>{logoHtml}</div>
        <h1>قرارداد</h1>
        <div class='meta'>شماره: {contract.ContractNumber} | تاریخ: {contract.StartDate:yyyy/MM/dd}</div>
    </div>

    <div class='print-body'>
        {bodyHtml}
    </div>

    <div class='print-footer'>
        <div class='signature-section'>
            <div class='signature-box'>
                <h4>محل امضا و مهر کارفرما (طرف اول)</h4>
                <p style='font-size:11px;color:#475569;'>{companyName}</p>
                <div class='line'></div>
                <span class='label'>نام و امضا - تاریخ</span>
            </div>
            <div class='signature-box'>
                <h4>محل امضا و مهر مجری/مشتری (طرف دوم)</h4>
                <p style='font-size:11px;color:#475569;'>{contract.ClientName}</p>
                <div class='line'></div>
                <span class='label'>نام و امضا - تاریخ</span>
            </div>
        </div>
    </div>

    <div class='watermark'>تولید شده توسط {companyName}</div>
</body>
</html>";
    }

    public async Task<string> PreviewTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default)
    {
        var template = await _context.ContractTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId && (t.OwnerUserId == null || t.OwnerUserId == currentUserId), ct);

        if (template?.HtmlContent == null)
            return GetDefaultTemplateHtml();

        return _templateRenderer.RenderPreview(template.HtmlContent);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Accounting Backfill
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Backfill: ايجاد سند درآمد معوق برای قراردادهای فعال پيش از اين قابليت.
    /// يک‌بار اجرا می‌شود.
    /// </summary>
    public async Task<(int Updated, int Errors)> BackfillDeferredRevenueForExistingAsync(string currentUserId, CancellationToken ct = default)
    {
        var activeContracts = await _context.Contracts
            .Where(c => c.Status == ContractStatuses.Active && c.TotalAmount > 0)
            .ToListAsync(ct);

        int updated = 0, errors = 0;

        foreach (var contract in activeContracts)
        {
            try
            {
                await _contractAccounting.RecordDeferredRevenueAsync(contract, currentUserId, ct);
                updated++;
                _logger.LogInformation("Backfill: deferred revenue booked for contract {Id} ({Number}).",
                    contract.Id, contract.ContractNumber);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Backfill failed for contract {Id} ({Number}).", contract.Id, contract.ContractNumber);
            }
        }

        _logger.LogInformation(
            "Deferred revenue backfill completed: {Updated} updated, {Errors} errors out of {Total} contracts.",
            updated, errors, activeContracts.Count);

        return (updated, errors);
    }

    private static string GetDefaultTemplateHtml()
    {
        return @"<!DOCTYPE html>
<html dir='rtl' lang='fa'>
<head><meta charset='utf-8'><title>قرارداد</title></head>
<body style='font-family: Vazirmatn, Tahoma, sans-serif; padding: 40px;'>
<h1 style='text-align:center;'>قرارداد</h1>
<p><strong>شماره:</strong> {{ContractNumber}}</p>
<p><strong>عنوان:</strong> {{Title}}</p>
<p><strong>نوع قرارداد:</strong> {{ContractType}}</p>
<hr/>
<h2>اطلاعات طرفین</h2>
<p><strong>مشتری:</strong> {{CustomerName}}</p>
<p><strong>شرکت:</strong> {{CustomerCompany}}</p>
<p><strong>تلفن:</strong> {{CustomerPhone}}</p>
<p><strong>آدرس:</strong> {{CustomerAddress}}</p>
<hr/>
<h2>اطلاعات قرارداد</h2>
<p><strong>تاریخ شروع:</strong> {{StartDate}}</p>
<p><strong>تاریخ پایان:</strong> {{EndDate}}</p>
<p><strong>تاریخ امضا:</strong> {{SignDate}}</p>
<p><strong>وضعیت:</strong> {{Status}}</p>
<hr/>
<h2>اطلاعات مالی</h2>
<p><strong>مبلغ کل:</strong> {{TotalAmount}} ریال</p>
<p><strong>مبلغ پرداخت شده:</strong> {{PaidAmount}} ریال</p>
<p><strong>باقی مانده:</strong> {{RemainingBalance}} ریال</p>
<p><strong>وضعیت پرداخت:</strong> {{PaymentStatus}}</p>
<hr/>
<h2>شرایط و ضوابط</h2>
<p>{{Terms}}</p>
<hr/>
<h2>توضیحات</h2>
<p>{{Description}}</p>
<hr/>
<p style='text-align:center;'><strong>{{CompanyName}}</strong></p>
</body>
</html>";
    }

    // ═══════════════════════════════════════════════════════════════
    //  Dashboard / Summary
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractDashboardVM> GetDashboardAsync(string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        var query = _context.Contracts.AsNoTracking();
        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);

        var now = DateTime.Today;
        var thirtyDaysFromNow = now.AddDays(30);

        var total = await query.CountAsync(ct);
        var active = await query.CountAsync(c => c.Status == ContractStatuses.Active && !c.IsArchived, ct);
        var expired = await query.CountAsync(c => c.Status == ContractStatuses.Expired, ct);
        var archived = await query.CountAsync(c => c.IsArchived, ct);
        var drafts = await query.CountAsync(c => c.Status == ContractStatuses.Draft, ct);
        var expiringSoon = await query.CountAsync(c => c.Status == ContractStatuses.ExpiringSoon, ct);

        var totalAmount = await query.SumAsync(c => c.TotalAmount, ct);

        // Compute TotalPaid from the actual ContractPayments table (subquery) rather than
        // relying on the denormalized Contract.PaidAmount column, so the dashboard always
        // reflects the true sum of all paid payments even when the denormalized field is
        // stale (e.g. after a direct DB update that bypassed AddPaymentAsync/DeletePaymentAsync).
        var contractIds = await query.Select(c => c.Id).ToListAsync(ct);
        var totalPaid = await _context.ContractPayments
            .Where(p => contractIds.Contains(p.ContractId) && p.Status == "Paid")
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        // Payment status counts: compute from the denormalized PaymentStatus field on Contract
        // (this is kept in sync by AddPaymentAsync / DeletePaymentAsync).
        var fullyPaid = await query.CountAsync(c => c.PaymentStatus == PaymentStatuses.FullyPaid && c.TotalAmount > 0, ct);
        var partiallyPaid = await query.CountAsync(c => c.PaymentStatus == PaymentStatuses.PartiallyPaid && c.TotalAmount > 0, ct);
        var unpaid = await query.CountAsync(c => (c.PaymentStatus == PaymentStatuses.Unpaid || c.PaidAmount <= 0) && c.TotalAmount > 0, ct);

        // ── Chart data: Status distribution ──
        var statusGroups = await query
            .GroupBy(c => c.Status)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // ── Chart data: Monthly value trend (last 12 months) ──
        var twelveMonthsAgo = now.AddMonths(-12);
        var monthlyGroups = await query
            .Where(c => c.SignDate >= twelveMonthsAgo || c.CreatedAt >= twelveMonthsAgo)
            .GroupBy(c => new { Year = (c.SignDate ?? c.CreatedAt).Year, Month = (c.SignDate ?? c.CreatedAt).Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(c => c.TotalAmount) })
            .ToListAsync(ct);

        // Build a complete 12-month series (fill missing months with 0)
        var monthlyTrend = new List<ChartDataPoint>();
        for (int i = 11; i >= 0; i--)
        {
            var monthDate = now.AddMonths(-i);
            var match = monthlyGroups.FirstOrDefault(m => m.Year == monthDate.Year && m.Month == monthDate.Month);
            monthlyTrend.Add(new ChartDataPoint
            {
                Label = monthDate.ToString("yyyy/MM"),
                Value = match?.Total ?? 0m
            });
        }

        // ── Chart data: Contract type distribution ──
        var typeGroups = await query
            .Where(c => c.ContractType != null && c.ContractType != "")
            .GroupBy(c => c.ContractType!)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new ContractDashboardVM
        {
            TotalContracts = total,
            ActiveContracts = active,
            ExpiredContracts = expired,
            ArchivedContracts = archived,
            DraftContracts = drafts,
            ExpiringSoonCount = expiringSoon,
            TotalAmount = totalAmount,
            TotalPaid = totalPaid,
            TotalOutstanding = totalAmount - totalPaid,
            FullyPaid = fullyPaid,
            PartiallyPaid = partiallyPaid,
            Unpaid = unpaid,

            StatusDistribution = statusGroups.Select(g => new ChartDataPoint { Label = g.Label, Value = g.Count }).ToList(),
            MonthlyValueTrend = monthlyTrend,
            ContractTypeDistribution = typeGroups.Select(g => new ChartDataPoint { Label = g.Label, Value = g.Count }).ToList(),
            PaymentSummary = new List<ChartDataPoint>
            {
                new() { Label = "مبلغ کل قراردادها", Value = totalAmount },
                new() { Label = "مبلغ پرداخت شده", Value = totalPaid },
                new() { Label = "باقی مانده", Value = totalAmount - totalPaid },
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  Authorization & RLS
    // ═══════════════════════════════════════════════════════════════

    public bool CanUserViewContract(Contract contract, string currentUserId, bool isAdmin)
    {
        if (isAdmin) return true;
        if (contract.AssignedUserId == currentUserId) return true;
        if (contract.CreatedByUserId == currentUserId) return true;
        return false;
    }

    public bool CanUserEditContract(Contract contract, string currentUserId, bool isAdmin)
    {
        if (isAdmin) return true;
        if (contract.AssignedUserId == currentUserId) return true;
        if (contract.CreatedByUserId == currentUserId) return true;
        return false;
    }

    public bool CanUserViewFinancials(Contract contract, string currentUserId, bool isAdmin)
    {
        if (isAdmin) return true;
        if (contract.AssignedUserId == currentUserId) return true;
        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Validation & Helpers
    // ═══════════════════════════════════════════════════════════════

    public async Task<bool> IsContractNumberUniqueAsync(string contractNumber, int? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.Contracts.AsNoTracking().Where(c => c.ContractNumber == contractNumber.Trim());
        if (excludeId.HasValue) query = query.Where(c => c.Id != excludeId.Value);
        return !await query.AnyAsync(ct);
    }

    public async Task<string> GenerateNextContractNumberAsync(CancellationToken ct = default)
    {
        var lastContract = await _context.Contracts.AsNoTracking()
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (lastContract == null)
            return "CTR-1403-001";

        var parts = lastContract.ContractNumber.Split('-');
        if (parts.Length == 3 && int.TryParse(parts[2], out int lastNum))
            return $"CTR-{parts[1]}-{(lastNum + 1):D3}";

        return $"CTR-1403-001";
    }

    // ═══════════════════════════════════════════════════════════════
    //  Customers CRUD
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<Customer>> GetCustomersAsync(CancellationToken ct = default)
    {
        return await _context.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken ct = default)
    {
        customer.CreatedAt = DateTime.Now;
        customer.UpdatedAt = DateTime.Now;
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Customer {Name} created", customer.Name);
        return customer;
    }

    public async Task<Customer> UpdateCustomerAsync(Customer customer, CancellationToken ct = default)
    {
        var existing = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id, ct)
            ?? throw new KeyNotFoundException($"Customer {customer.Id} not found.");
        existing.Name = customer.Name;
        existing.EconomicCode = customer.EconomicCode;
        existing.NationalId = customer.NationalId;
        existing.Phone = customer.Phone;
        existing.Address = customer.Address;
        existing.Email = customer.Email;
        existing.CompanyName = customer.CompanyName;
        existing.IsActive = customer.IsActive;
        existing.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
        return existing;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Dynamic Form Fields (EAV)
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<ContractTypeFieldVM>> GetAllTypeFieldDefinitionsAsync(CancellationToken ct = default)
    {
        return await _context.ContractTypeFields
            .AsNoTracking()
            .OrderBy(f => f.ContractType)
            .ThenBy(f => f.SortOrder)
            .Select(f => new ContractTypeFieldVM
            {
                Id = f.Id,
                ContractType = f.ContractType,
                FieldKey = f.FieldKey,
                DisplayName = f.DisplayName,
                FieldType = f.FieldType,
                Options = f.Options,
                IsRequired = f.IsRequired,
                SortOrder = f.SortOrder,
                SectionName = f.SectionName,
                Placeholder = f.Placeholder,
                ValidationPattern = f.ValidationPattern,
                ValidationMessage = f.ValidationMessage
            })
            .ToListAsync(ct);
    }

    public async Task SaveFieldValuesAsync(int contractId, string contractType, Dictionary<string, string> fieldValues, CancellationToken ct = default)
    {
        // Remove existing values
        var existing = await _context.ContractFieldValues
            .Where(v => v.ContractId == contractId)
            .ToListAsync(ct);
        _context.ContractFieldValues.RemoveRange(existing);

        if (fieldValues == null || fieldValues.Count == 0)
            return;

        // Get the field definitions for this contract type
        var definitions = await _context.ContractTypeFields
            .AsNoTracking()
            .Where(f => f.ContractType == contractType)
            .ToListAsync(ct);

        var fieldDefMap = definitions.ToDictionary(f => f.FieldKey, f => f.Id);

        var values = new List<ContractFieldValue>();
        foreach (var kvp in fieldValues)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                continue;
            if (!fieldDefMap.TryGetValue(kvp.Key, out var defId))
                continue;

            values.Add(new ContractFieldValue
            {
                ContractId = contractId,
                ContractTypeFieldId = defId,
                Value = kvp.Value?.Trim()
            });
        }

        if (values.Count > 0)
        {
            _context.ContractFieldValues.AddRange(values);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<List<ContractTypeFieldVM>> LoadFieldValuesAsync(int contractId, CancellationToken ct = default)
    {
        var defs = await GetAllTypeFieldDefinitionsAsync(ct);
        var defLookup = defs.ToDictionary(f => f.Id, f => f);

        var savedValues = await _context.ContractFieldValues
            .AsNoTracking()
            .Where(v => v.ContractId == contractId)
            .ToListAsync(ct);

        foreach (var sv in savedValues)
        {
            if (defLookup.TryGetValue(sv.ContractTypeFieldId, out var def))
            {
                def.Value = sv.Value;
            }
        }

        return defs;
    }

    private static string ToPersianDate(DateTime dt)
    {
        try
        {
            var pc = new System.Globalization.PersianCalendar();
            return $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00}";
        }
        catch
        {
            return dt.ToString("yyyy/MM/dd");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Bulk Actions
    // ═══════════════════════════════════════════════════════════════

    public async Task<BulkActionResult> BulkActionAsync(BulkActionRequest request, string currentUserId, CancellationToken ct = default)
    {
        if (request.IDs == null || request.IDs.Count == 0)
        {
            return new BulkActionResult { Message = "هیچ قراردادی انتخاب نشده است." };
        }

        var isAdmin = await IsAdminAsync(currentUserId);
        var result = new BulkActionResult();
        var errors = new List<string>();

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var id in request.IDs)
            {
                try
                {
                    var contract = await _context.Contracts
                        .FirstOrDefaultAsync(c => c.Id == id, ct);

                    if (contract == null)
                    {
                        errors.Add($"قرارداد با شناسه {id} یافت نشد.");
                        continue;
                    }

                    if (!CanUserEditContract(contract, currentUserId, isAdmin))
                    {
                        errors.Add($"شما به قرارداد {contract.ContractNumber} دسترسی ندارید.");
                        continue;
                    }
                    switch (request.Action.ToLowerInvariant())
                    {
                        case "status":
                            if (string.IsNullOrWhiteSpace(request.NewStatus))
                            {
                                errors.Add($"وضعیت جدید برای قرارداد {contract.ContractNumber} مشخص نشده است.");
                                continue;
                            }
                            var oldStatus = contract.Status;
                            await ValidateContractStateTransition(contract.Status, request.NewStatus, isAdmin, currentUserId, contract.Id);
                            if (contract.Status == ContractStatuses.Draft && request.NewStatus == ContractStatuses.PendingApproval)
                            {
                                await EnforceDocumentRequirementsAsync(contract.Id);
                            }
                            contract.Status = request.NewStatus;
                            contract.UpdatedAt = DateTime.Now;
                            await AddAuditLogAsync(contract.Id, currentUserId, "StatusChanged", oldStatus, request.NewStatus,
                                $"تغییر وضعیت گروهی از {oldStatus} به {request.NewStatus}", ct);
                            result.SuccessCount++;
                            break;

                        case "assign":
                            if (string.IsNullOrWhiteSpace(request.AssignToUserId))
                            {
                                errors.Add($"کاربر مسئول برای قرارداد {contract.ContractNumber} مشخص نشده است.");
                                continue;
                            }
                            var oldAssignee = contract.AssignedUserId;
                            contract.AssignedUserId = request.AssignToUserId;
                            contract.UpdatedAt = DateTime.Now;
                            await AddAuditLogAsync(contract.Id, currentUserId, "Assigned", oldAssignee, request.AssignToUserId,
                                $"تغییر مسئول گروهی", ct);
                            result.SuccessCount++;
                            break;

                        case "archive":
                            if (contract.IsArchived)
                            {
                                errors.Add($"قرارداد {contract.ContractNumber} قبلاً بایگانی شده است.");
                                continue;
                            }
                            contract.IsArchived = true;
                            contract.ArchivedAt = DateTime.Now;
                            contract.ArchivedByUserId = currentUserId;
                            contract.Status = ContractStatuses.Archived;
                            contract.UpdatedAt = DateTime.Now;
                            await AddAuditLogAsync(contract.Id, currentUserId, "Archived", null, ContractStatuses.Archived,
                                "بایگانی گروهی", ct);
                            result.SuccessCount++;
                            break;

                        case "delete":
                            _context.Contracts.Remove(contract);
                            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts", id.ToString());
                            try
                            {
                                if (Directory.Exists(uploadsDir))
                                    Directory.Delete(uploadsDir, recursive: true);
                            }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
                            {
                                _logger.LogWarning(ex, "Failed to delete upload directory for contract {ContractId} during bulk delete", id);
                            }
                            result.SuccessCount++;
                            break;

                        default:
                            errors.Add($"عملیات '{request.Action}' نامعتبر است.");
                            continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Bulk action failed for contract {ContractId}", id);
                    errors.Add($"خطا در پردازش قرارداد {id}: {ex.Message}");
                    continue;
                }
            }

            if (result.SuccessCount > 0)
            {
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                result.Message = $"{result.SuccessCount} قرارداد با موفقیت پردازش شد.";
            }
            else
            {
                await transaction.RollbackAsync(ct);
                result.Message = "هیچ قراردادی پردازش نشد.";
            }

            result.FailureCount = errors.Count;
            result.Errors = errors;
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Bulk action transaction failed");
            return new BulkActionResult
            {
                Message = "عملیات گروهی با خطا مواجه شد. تمام تغییرات لغو شدند.",
                FailureCount = request.IDs.Count,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Auto-Renewal Logic
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Processes all contracts that are due for auto-renewal.
    /// A contract qualifies for auto-renewal when:
    ///   - IsAutoRenew == true
    ///   - Status is Active or ExpiringSoon
    ///   - Not cancelled (AutoRenewalCancelledAt == null)
    ///   - EndDate is within the next AutoRenewDaysBeforeExpiry days (or today/expired)
    ///   - Has not already been auto-renewed past this EndDate (LastAutoRenewedAt check)
    ///
    /// For each qualifying contract, a new contract period is created by extending
    /// the EndDate by AutoRenewTermMonths. An AutoRenewalLog entry is recorded.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of contracts renewed</returns>
    public async Task<int> ProcessAutoRenewalsAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var renewals = await _context.Contracts
            .Where(c => c.IsAutoRenew
                && c.AutoRenewalCancelledAt == null
                && c.EndDate != null
                && (c.Status == ContractStatuses.Active
                    || c.Status == ContractStatuses.ExpiringSoon
                    || c.Status == ContractStatuses.Expired)
                && c.EndDate.Value.Date <= today.AddDays(c.AutoRenewDaysBeforeExpiry)
                && (c.LastAutoRenewedAt == null || c.LastAutoRenewedAt.Value.Date < c.EndDate.Value.Date))
            .ToListAsync(ct);

        int renewedCount = 0;
        foreach (var contract in renewals)
        {
            try
            {
                var previousEndDate = contract.EndDate!.Value;
                var newEndDate = previousEndDate.AddMonths(contract.AutoRenewTermMonths);

                contract.EndDate = newEndDate;
                contract.LastAutoRenewedAt = DateTime.UtcNow;
                contract.Status = ContractStatuses.Active;
                contract.UpdatedAt = DateTime.UtcNow;

                var log = new AutoRenewalLog
                {
                    ContractId = contract.Id,
                    RenewedContractId = null,
                    RenewedAt = DateTime.UtcNow,
                    PreviousEndDate = previousEndDate,
                    NewEndDate = newEndDate,
                    RenewalTermMonths = contract.AutoRenewTermMonths,
                    Status = "Success",
                    Notes = $"تمدید خودکار قرارداد به مدت {contract.AutoRenewTermMonths} ماه",
                    CreatedAt = DateTime.UtcNow
                };

                _context.AutoRenewalLogs.Add(log);
                _logger.LogInformation(
                    "Auto-renewed contract {ContractId} ({ContractNumber}): EndDate extended from {OldEndDate:yyyy-MM-dd} to {NewEndDate:yyyy-MM-dd}",
                    contract.Id, contract.ContractNumber, previousEndDate, newEndDate);

                renewedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-renew contract {ContractId} ({ContractNumber})",
                    contract.Id, contract.ContractNumber);
            }
        }

        if (renewedCount > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        return renewedCount;
    }

    /// <summary>
    /// Cancels the auto-renewal for a contract before it executes.
    /// Sets AutoRenewalCancelledAt and records a log entry.
    /// </summary>
    public async Task<bool> CancelAutoRenewalAsync(int contractId, string currentUserId, CancellationToken ct = default)
    {
        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null)
            return false;

        if (!contract.IsAutoRenew)
            return false; // Already not set for auto-renewal

        if (contract.AutoRenewalCancelledAt != null)
            return false; // Already cancelled

        contract.IsAutoRenew = false;
        contract.AutoRenewalCancelledAt = DateTime.UtcNow;
        contract.AutoRenewalCancelledByUserId = currentUserId;
        contract.UpdatedAt = DateTime.UtcNow;

        var log = new AutoRenewalLog
        {
            ContractId = contract.Id,
            RenewedContractId = null,
            RenewedAt = DateTime.UtcNow,
            PreviousEndDate = contract.EndDate,
            NewEndDate = contract.EndDate,
            RenewalTermMonths = contract.AutoRenewTermMonths,
            Status = "CancelledByUser",
            Notes = "تمدید خودکار توسط کاربر لغو شد",
            CancelledByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AutoRenewalLogs.Add(log);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Auto-renewal cancelled for contract {ContractId} ({ContractNumber}) by user {UserId}",
            contract.Id, contract.ContractNumber, currentUserId);

        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Column Customization
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Saves the user's column visibility preferences for the contracts list.
    /// </summary>
    public async Task SaveColumnPreferenceAsync(string userId, string[] columnNames, CancellationToken ct = default)
    {
        var settings = await _context.ContractUserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (settings == null)
        {
            settings = new ContractUserSetting
            {
                UserId = userId,
                DefaultStatus = ContractStatuses.Draft,
                SelectedColumns = System.Text.Json.JsonSerializer.Serialize(columnNames)
            };
            _context.ContractUserSettings.Add(settings);
        }
        else
        {
            settings.SelectedColumns = System.Text.Json.JsonSerializer.Serialize(columnNames);
            settings.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets the user's saved column names for the contracts list.
    /// Returns null if no preference has been saved.
    /// </summary>
    public async Task<string[]?> GetColumnPreferenceAsync(string userId, CancellationToken ct = default)
    {
        var settings = await _context.ContractUserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (settings == null || string.IsNullOrEmpty(settings.SelectedColumns))
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(settings.SelectedColumns);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// محاسبه مبالغ مالیات، مبلغ نهایی و مبلغ به ارز پایه
    /// </summary>
    private static (decimal totalTax, decimal totalInclusive, decimal baseCurrencyAmount) CalculateTaxAndCurrency(
        decimal totalAmount, string? taxMethod, decimal taxRatePercent, string? currencyCode, decimal exchangeRate)
    {
        var method = string.IsNullOrWhiteSpace(taxMethod) ? TaxCalculationMethod.Exclusive : taxMethod;
        decimal taxAmount;
        decimal inclusiveAmount;

        if (string.Equals(method, TaxCalculationMethod.Exclusive, StringComparison.OrdinalIgnoreCase))
        {
            taxAmount = Math.Round(totalAmount * (taxRatePercent / 100m), 2);
            inclusiveAmount = totalAmount + taxAmount;
        }
        else if (string.Equals(method, TaxCalculationMethod.Inclusive, StringComparison.OrdinalIgnoreCase))
        {
            inclusiveAmount = totalAmount;
            taxAmount = taxRatePercent > 0
                ? Math.Round(totalAmount - (totalAmount / (1m + taxRatePercent / 100m)), 2)
                : 0m;
        }
        else // None
        {
            taxAmount = 0m;
            inclusiveAmount = totalAmount;
        }

        var rate = exchangeRate > 0 ? exchangeRate : 1m;
        var baseCurrency = Math.Round(totalAmount * rate, 2);

        return (taxAmount, inclusiveAmount, baseCurrency);
    }
}

