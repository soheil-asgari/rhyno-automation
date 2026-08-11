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

    public ContractService(
        OfficeDbContext context,
        UserManager<User> userManager,
        ILogger<ContractService> logger,
        IAuditLogger auditLogger,
        ITemplateRenderingService templateRenderer,
        ITenantSettingsService tenantSettings)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _auditLogger = auditLogger;
        _templateRenderer = templateRenderer;
        _tenantSettings = tenantSettings;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private async Task<bool> IsAdminAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;
        return _userManager.IsInRoleAsync(user, "Admin").Result;
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
    private async Task AddAuditLogAsync(int contractId, string userId, string action, string? oldStatus, string? newStatus, string? description = null, CancellationToken ct = default)
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
                CreatedAt = c.CreatedAt
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
            .Include(x => x.AuditLogs).ThenInclude(al => al.User);

        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);

        var c = await query.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Contract {id} not found.");

        var daysRemaining = c.EndDate.HasValue ? (c.EndDate.Value - DateTime.Today).Days : 0;

        return new ContractDetailsVM
        {
            Id = c.Id,
            ContractNumber = c.ContractNumber,
            Title = c.Title,
            ContractType = c.ContractType,
            ClientName = c.ClientName,
            ClientPhone = c.ClientPhone,
            ClientAddress = c.ClientAddress,
            CustomerId = c.CustomerId,
            CustomerName = c.Customer?.Name,
            Description = c.Description,
            TermsAndConditions = c.TermsAndConditions,
            ContractBodyHtml = c.ContractBodyHtml,
            TotalAmount = c.TotalAmount,
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
            CreatedByUserName = c.CreatedByUser?.FullName ?? c.CreatedByUser?.UserName ?? "نامشخص",
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            DaysRemaining = daysRemaining,
            CanEdit = CanUserEditContract(c, currentUserId, isAdmin),
            CanViewFinancials = CanUserViewFinancials(c, currentUserId, isAdmin),
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new ContractPaymentVM
            {
                Id = p.Id, Amount = p.Amount, PaymentDate = p.PaymentDate, DueDate = p.DueDate,
                Status = p.Status, PaymentMethod = p.PaymentMethod, ReferenceNumber = p.ReferenceNumber,
                Notes = p.Notes, CreatedAt = p.CreatedAt
            }).ToList(),
            Attachments = c.Attachments.OrderByDescending(a => a.CreatedAt).Select(a => new ContractAttachmentVM
            {
                Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, FileSize = a.FileSize, CreatedAt = a.CreatedAt
            }).ToList(),
            AuditLogs = c.AuditLogs.OrderByDescending(al => al.CreatedAt).Select(al => new ContractAuditLogVM
            {
                Id = al.Id, UserName = al.User != null ? al.User.FullName ?? al.User.UserName ?? "نامشخص" : "نامشخص",
                Action = al.Action, OldStatus = al.OldStatus, NewStatus = al.NewStatus,
                Description = al.Description, CreatedAt = al.CreatedAt
            }).ToList()
        };
    }

    public async Task<ContractCreateVM> BuildCreateViewModelAsync(string currentUserId, CancellationToken ct = default)
    {
        var contractNumber = await GenerateNextContractNumberAsync(ct);
        return new ContractCreateVM
        {
            ContractNumber = contractNumber,
            StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s }).ToList(),
            ContractTypeOptions = ContractTypes.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s }).ToList(),
            UserOptions = await GetUserSelectListAsync(null, ct),
            TemplateOptions = await GetTemplateSelectListAsync(currentUserId, null, ct),
            CustomerOptions = await GetCustomerSelectListAsync(null, ct)
        };
    }

    public async Task<Contract> CreateContractAsync(ContractCreateVM model, string currentUserId, CancellationToken ct = default)
    {
        // Auto-generate contract number if not provided
        if (string.IsNullOrWhiteSpace(model.ContractNumber))
        {
            model.ContractNumber = await GenerateNextContractNumberAsync(ct);
        }

        // Validate unique contract number
        if (!await IsContractNumberUniqueAsync(model.ContractNumber, null, ct))
            throw new InvalidOperationException("شماره قرارداد تکراری است.");

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
            TermsAndConditions = model.TermsAndConditions,
            ContractBodyHtml = model.ContractBodyHtml,
            TotalAmount = model.TotalAmount,
            PaidAmount = 0,
            RemainingBalance = model.TotalAmount,
            PaymentStatus = PaymentStatuses.Unpaid,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            SignDate = model.SignDate,
            ExpiryNoticeDays = model.ExpiryNoticeDays > 0 ? model.ExpiryNoticeDays : 30,
            Status = model.Status ?? ContractStatuses.Draft,
            AssignedUserId = model.AssignedUserId,
            TemplateId = model.TemplateId,
            CreatedByUserId = currentUserId,
            IsArchived = false,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
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
                contract.ContractBodyHtml = _templateRenderer.RenderTemplate(template.HtmlContent, contract, customer, assignedUser);
            }
        }

        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync(ct);

        // Log creation
        await AddAuditLogAsync(contract.Id, currentUserId, "Created", null, contract.Status, "قرارداد ایجاد شد", ct);

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
            TermsAndConditions = c.TermsAndConditions,
            ContractBodyHtml = c.ContractBodyHtml,
            TotalAmount = c.TotalAmount,
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
            StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == c.Status }).ToList(),
            ContractTypeOptions = ContractTypes.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == c.ContractType }).ToList(),
            UserOptions = await GetUserSelectListAsync(c.AssignedUserId, ct),
            TemplateOptions = await GetTemplateSelectListAsync(currentUserId, c.TemplateId, ct),
            CustomerOptions = await GetCustomerSelectListAsync(c.CustomerId, ct)
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

        var oldStatus = c.Status;

        c.ContractNumber = model.ContractNumber.Trim();
        c.Title = model.Title.Trim();
        c.ContractType = model.ContractType;
        c.ClientName = model.ClientName.Trim();
        c.ClientPhone = model.ClientPhone?.Trim();
        c.ClientAddress = model.ClientAddress?.Trim();
        c.CustomerId = model.CustomerId;
        c.Description = model.Description?.Trim();
        c.TermsAndConditions = model.TermsAndConditions;
        c.ContractBodyHtml = model.ContractBodyHtml;
        c.TotalAmount = model.TotalAmount;
        c.StartDate = model.StartDate;
        c.EndDate = model.EndDate;
        c.SignDate = model.SignDate;
        c.ExpiryNoticeDays = model.ExpiryNoticeDays > 0 ? model.ExpiryNoticeDays : 30;
        c.Status = model.Status;
        c.AssignedUserId = model.AssignedUserId;
        c.TemplateId = model.TemplateId;
        c.UpdatedAt = DateTime.Now;

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

        // Log status change if changed
        if (oldStatus != model.Status)
        {
            await AddAuditLogAsync(c.Id, currentUserId, "StatusChanged", oldStatus, model.Status, $"تغییر وضعیت از {oldStatus} به {model.Status}", ct);
        }
        else
        {
            await AddAuditLogAsync(c.Id, currentUserId, "Edited", null, null, "قرارداد ویرایش شد", ct);
        }

        _logger.LogInformation("Contract {ContractNumber} updated by {UserId}", c.ContractNumber, currentUserId);
        return c;
    }

    public async Task<bool> DeleteContractAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var c = await _context.Contracts.Include(x => x.Payments).Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return false;
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
        c.IsArchived = false; c.ArchivedAt = null; c.ArchivedByUserId = null;
        c.Status = ContractStatuses.Active; c.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
        await AddAuditLogAsync(c.Id, currentUserId, "Unarchived", ContractStatuses.Archived, ContractStatuses.Active, "قرارداد از بایگانی خارج شد", ct);
        _logger.LogInformation("Contract {ContractNumber} unarchived by {UserId}", c.ContractNumber, currentUserId);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Payments
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractPayment> AddPaymentAsync(ContractPaymentCreateVM model, CancellationToken ct = default)
    {
        var contract = await _context.Contracts.FindAsync(new object[] { model.ContractId }, ct)
            ?? throw new KeyNotFoundException($"Contract {model.ContractId} not found.");

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
            CreatedAt = DateTime.Now
        };
        _context.ContractPayments.Add(payment);

        // Update contract financials
        contract.PaidAmount = contract.Payments.Sum(p => p.Amount) + model.Amount;
        contract.RemainingBalance = contract.TotalAmount - contract.PaidAmount;

        if (contract.RemainingBalance <= 0 && contract.TotalAmount > 0)
            contract.PaymentStatus = PaymentStatuses.FullyPaid;
        else if (contract.PaidAmount > 0)
            contract.PaymentStatus = PaymentStatuses.PartiallyPaid;

        contract.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Payment {Amount} added to contract {ContractId}", model.Amount, model.ContractId);
        return payment;
    }

    public async Task<bool> DeletePaymentAsync(int paymentId, int contractId, CancellationToken ct = default)
    {
        var payment = await _context.ContractPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.ContractId == contractId, ct);
        if (payment == null) return false;

        _context.ContractPayments.Remove(payment);

        // Recalculate contract financials
        var contract = await _context.Contracts.FindAsync(new object[] { contractId }, ct);
        if (contract != null)
        {
            contract.PaidAmount = contract.Payments.Sum(p => p.Amount) - payment.Amount;
            contract.RemainingBalance = contract.TotalAmount - contract.PaidAmount;

            if (contract.RemainingBalance <= 0 && contract.TotalAmount > 0)
                contract.PaymentStatus = PaymentStatuses.FullyPaid;
            else if (contract.PaidAmount > 0)
                contract.PaymentStatus = PaymentStatuses.PartiallyPaid;
            else
                contract.PaymentStatus = PaymentStatuses.Unpaid;

            contract.UpdatedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Attachments
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractAttachment> UploadAttachmentAsync(int contractId, string fileName, Stream fileStream, string contentType, string uploadedByUserId, CancellationToken ct = default)
    {
        var contract = await _context.Contracts.FindAsync(new object[] { contractId }, ct)
            ?? throw new KeyNotFoundException($"Contract {contractId} not found.");

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts", contractId.ToString());
        Directory.CreateDirectory(uploadsDir);

        var storedFileName = $"{Guid.NewGuid():N}_{fileName}";
        var filePath = Path.Combine(uploadsDir, storedFileName);

        await using (var fs = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        var attachment = new ContractAttachment
        {
            ContractId = contractId,
            FileName = fileName,
            StoredFileName = storedFileName,
            RelativePath = $"/uploads/contracts/{contractId}/{storedFileName}",
            ContentType = contentType,
            FileSize = new FileInfo(filePath).Length,
            UploadedByUserId = uploadedByUserId,
            CreatedAt = DateTime.Now
        };

        _context.ContractAttachments.Add(attachment);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Attachment {FileName} uploaded to contract {ContractId}", fileName, contractId);
        return attachment;
    }

    public async Task<ContractAttachment?> GetAttachmentAsync(int attachmentId, CancellationToken ct = default)
    {
        return await _context.ContractAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
    }

    public async Task<bool> DeleteAttachmentAsync(int attachmentId, int contractId, CancellationToken ct = default)
    {
        var attachment = await _context.ContractAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ContractId == contractId, ct);
        if (attachment == null) return false;

        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.RelativePath.TrimStart('/'));
        if (File.Exists(fullPath)) File.Delete(fullPath);

        _context.ContractAttachments.Remove(attachment);
        await _context.SaveChangesAsync(ct);
        return true;
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
        template.HtmlContent = model.HtmlContent;
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
        var totalPaid = await query.SumAsync(c => c.PaidAmount, ct);

        var fullyPaid = await query.CountAsync(c => c.PaymentStatus == PaymentStatuses.FullyPaid && c.TotalAmount > 0, ct);
        var partiallyPaid = await query.CountAsync(c => c.PaymentStatus == PaymentStatuses.PartiallyPaid && c.TotalAmount > 0, ct);
        var unpaid = await query.CountAsync(c => (c.PaymentStatus == PaymentStatuses.Unpaid || c.PaidAmount <= 0) && c.TotalAmount > 0, ct);

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
            Unpaid = unpaid
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
}