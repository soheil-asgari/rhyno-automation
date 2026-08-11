using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services.Auditing;

namespace OfficeAutomation.Services.Contracts;

public sealed class ContractService : IContractService
{
    private readonly OfficeDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ContractService> _logger;
    private readonly IAuditLogger _auditLogger;

    public ContractService(
        OfficeDbContext context,
        UserManager<User> userManager,
        ILogger<ContractService> logger,
        IAuditLogger auditLogger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _auditLogger = auditLogger;
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
    /// Base query that enforces visibility: Admins see all; assigned/creator users see their own.
    /// </summary>
    private IQueryable<Contract> ApplyVisibilityFilter(IQueryable<Contract> query, string currentUserId, bool isAdmin)
    {
        if (isAdmin) return query;
        return query.Where(c => c.ResponsibleUserId == currentUserId || c.CreatedByUserId == currentUserId);
    }

    private async Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetUserSelectListAsync(string? selectedUserId, CancellationToken ct)
    {
        return await _userManager.Users.OrderBy(u => u.FullName)
            .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u.Id, Text = u.FullName ?? u.UserName ?? u.Email ?? u.Id, Selected = u.Id == selectedUserId })
            .ToListAsync(ct);
    }

    private async Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetTemplateSelectListAsync(string currentUserId, int? selectedTemplateId, CancellationToken ct)
    {
        return await _context.ContractTemplates.AsNoTracking().Where(t => t.UserId == currentUserId)
            .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = t.Id.ToString(), Text = t.Name + (t.IsDefault ? " (پیش‌فرض)" : ""), Selected = t.Id == selectedTemplateId })
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

    // ═══════════════════════════════════════════════════════════════
    //  Contract CRUD
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractIndexVM> GetIndexViewModelAsync(ContractIndexVM filter, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);

        var query = _context.Contracts.AsNoTracking()
            .Include(c => c.ResponsibleUser)
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
        if (!string.IsNullOrWhiteSpace(filter.ResponsibleUserId)) query = query.Where(c => c.ResponsibleUserId == filter.ResponsibleUserId);
        if (!string.IsNullOrWhiteSpace(filter.ClientName)) query = query.Where(c => c.ClientName.Contains(filter.ClientName));
        if (!string.IsNullOrWhiteSpace(filter.ContractType)) query = query.Where(c => c.ContractType == filter.ContractType);
        if (filter.StartDateFrom.HasValue) query = query.Where(c => c.StartDate >= filter.StartDateFrom.Value);
        if (filter.StartDateTo.HasValue) query = query.Where(c => c.StartDate <= filter.StartDateTo.Value);
        if (filter.EndDateFrom.HasValue) query = query.Where(c => c.EndDate >= filter.EndDateFrom.Value);
        if (filter.EndDateTo.HasValue) query = query.Where(c => c.EndDate <= filter.EndDateTo.Value);
        if (filter.ExpirationDateFrom.HasValue) query = query.Where(c => c.ExpirationDate >= filter.ExpirationDateFrom.Value);
        if (filter.ExpirationDateTo.HasValue) query = query.Where(c => c.ExpirationDate <= filter.ExpirationDateTo.Value);
        if (filter.CustomerId.HasValue) query = query.Where(c => c.CustomerId == filter.CustomerId.Value);
        if (filter.MinAmount.HasValue) query = query.Where(c => c.TotalAmount >= filter.MinAmount.Value);
        if (filter.MaxAmount.HasValue) query = query.Where(c => c.TotalAmount <= filter.MaxAmount.Value);

        // Payment status filters
        if (filter.FullyPaidOnly == true) query = query.Where(c => c.PaidAmount >= c.TotalAmount);
        if (filter.UnpaidOnly == true) query = query.Where(c => c.PaidAmount <= 0);
        if (filter.PartiallyPaidOnly == true) query = query.Where(c => c.PaidAmount > 0 && c.PaidAmount < c.TotalAmount);

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
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                ResponsibleUserName = c.ResponsibleUser != null ? c.ResponsibleUser.FullName : null,
                ResponsibleUserId = c.ResponsibleUserId,
                IsArchived = c.IsArchived,
                IsOwnedByCurrentUser = c.ResponsibleUserId == currentUserId || c.CreatedByUserId == currentUserId,
                CreatedAt = c.CreatedAt
            }).ToListAsync(ct);

        // Summary stats (visible to admin only for full sums)
        var allQuery = _context.Contracts.AsNoTracking();
        allQuery = ApplyVisibilityFilter(allQuery, currentUserId, isAdmin);

        filter.TotalCount = await allQuery.CountAsync(ct);
        filter.ActiveCount = await allQuery.CountAsync(c => c.Status == ContractStatuses.Active, ct);
        filter.CompletedCount = await allQuery.CountAsync(c => c.Status == ContractStatuses.Completed, ct);
        filter.DraftCount = await allQuery.CountAsync(c => c.Status == ContractStatuses.Draft, ct);
        filter.FilteredCount = totalFiltered;
        filter.TotalAmountSum = await allQuery.SumAsync(c => c.TotalAmount, ct);
        filter.TotalPaidSum = await allQuery.SumAsync(c => c.PaidAmount, ct);
        filter.Page = page;
        filter.PageSize = pageSize;
        filter.TotalPages = totalPages;

        // Dropdown options
        filter.StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == filter.Status }).ToList();
        filter.ContractTypeOptions = ContractTypes.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == filter.ContractType }).ToList();
        filter.ResponsibleUserOptions = await GetUserSelectListAsync(filter.ResponsibleUserId, ct);
        filter.CustomerOptions = await GetCustomerSelectListAsync(filter.CustomerId, ct);
        filter.Items = items;
        return filter;
    }

    public async Task<Contract?> GetContractByIdAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        IQueryable<Contract> query = _context.Contracts.AsNoTracking()
            .Include(c => c.ResponsibleUser).Include(c => c.CreatedByUser)
            .Include(c => c.Customer)
            .Include(c => c.ContractTemplate).Include(c => c.Payments).Include(c => c.Attachments);

        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);
        return await query.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<ContractDetailsVM> GetDetailsViewModelAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        IQueryable<Contract> query = _context.Contracts.AsNoTracking()
            .Include(x => x.ResponsibleUser).Include(x => x.CreatedByUser)
            .Include(x => x.Customer)
            .Include(x => x.ContractTemplate).Include(x => x.Payments).Include(x => x.Attachments);

        query = ApplyVisibilityFilter(query, currentUserId, isAdmin);

        var c = await query.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Contract {id} not found.");

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
            Notes = c.Notes,
            TotalAmount = c.TotalAmount,
            PaidAmount = c.PaidAmount,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            SigningDate = c.SigningDate,
            RenewalDate = c.RenewalDate,
            ExpirationDate = c.ExpirationDate,
            Status = c.Status,
            ResponsibleUserName = c.ResponsibleUser?.FullName ?? c.ResponsibleUser?.UserName,
            ResponsibleUserId = c.ResponsibleUserId,
            TemplateName = c.ContractTemplate?.Name,
            IsArchived = c.IsArchived,
            ArchivedAt = c.ArchivedAt,
            CreatedByUserName = c.CreatedByUser?.FullName ?? c.CreatedByUser?.UserName ?? "نامشخص",
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            CanEdit = CanUserEditContract(c, currentUserId, isAdmin),
            CanViewFinancials = CanUserViewFinancials(c, currentUserId, isAdmin),
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new ContractPaymentVM { Id = p.Id, Amount = p.Amount, PaymentDate = p.PaymentDate, PaymentMethod = p.PaymentMethod, ReferenceNumber = p.ReferenceNumber, Description = p.Description, CreatedAt = p.CreatedAt }).ToList(),
            Attachments = c.Attachments.OrderByDescending(a => a.CreatedAt).Select(a => new ContractAttachmentVM { Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, FileSize = a.FileSize, CreatedAt = a.CreatedAt }).ToList()
        };
    }

    public async Task<ContractCreateVM> BuildCreateViewModelAsync(string currentUserId, CancellationToken ct = default)
    {
        return new ContractCreateVM
        {
            StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s }).ToList(),
            ContractTypeOptions = ContractTypes.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s }).ToList(),
            UserOptions = await GetUserSelectListAsync(null, ct),
            TemplateOptions = await GetTemplateSelectListAsync(currentUserId, null, ct),
            CustomerOptions = await GetCustomerSelectListAsync(null, ct)
        };
    }

    public async Task<Contract> CreateContractAsync(ContractCreateVM model, string currentUserId, CancellationToken ct = default)
    {
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
            Notes = model.Notes,
            TotalAmount = model.TotalAmount,
            PaidAmount = 0,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            SigningDate = model.SigningDate,
            RenewalDate = model.RenewalDate,
            ExpirationDate = model.ExpirationDate,
            Status = model.Status ?? ContractStatuses.Draft,
            ResponsibleUserId = model.ResponsibleUserId,
            ContractTemplateId = model.ContractTemplateId,
            CreatedByUserId = currentUserId,
            IsArchived = false,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Contract {ContractNumber} created by {UserId}", contract.ContractNumber, currentUserId);
        return contract;
    }

    public async Task<ContractEditVM> BuildEditViewModelAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var c = await _context.Contracts.AsNoTracking().Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id, ct)
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
            Notes = c.Notes,
            TotalAmount = c.TotalAmount,
            PaidAmount = c.PaidAmount,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            SigningDate = c.SigningDate,
            RenewalDate = c.RenewalDate,
            ExpirationDate = c.ExpirationDate,
            Status = c.Status,
            ResponsibleUserId = c.ResponsibleUserId,
            ContractTemplateId = c.ContractTemplateId,
            StatusOptions = ContractStatuses.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == c.Status }).ToList(),
            ContractTypeOptions = ContractTypes.All.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s, Text = s, Selected = s == c.ContractType }).ToList(),
            UserOptions = await GetUserSelectListAsync(c.ResponsibleUserId, ct),
            TemplateOptions = await GetTemplateSelectListAsync(currentUserId, c.ContractTemplateId, ct),
            CustomerOptions = await GetCustomerSelectListAsync(c.CustomerId, ct)
        };
    }

    public async Task<Contract> UpdateContractAsync(ContractEditVM model, string currentUserId, CancellationToken ct = default)
    {
        // Validate unique contract number (excluding current)
        if (!await IsContractNumberUniqueAsync(model.ContractNumber, model.Id, ct))
            throw new InvalidOperationException("شماره قرارداد تکراری است.");

        var c = await _context.Contracts.FirstOrDefaultAsync(x => x.Id == model.Id, ct)
            ?? throw new KeyNotFoundException($"Contract {model.Id} not found.");

        c.ContractNumber = model.ContractNumber.Trim();
        c.Title = model.Title.Trim();
        c.ContractType = model.ContractType;
        c.ClientName = model.ClientName.Trim();
        c.ClientPhone = model.ClientPhone?.Trim();
        c.ClientAddress = model.ClientAddress?.Trim();
        c.CustomerId = model.CustomerId;
        c.Description = model.Description?.Trim();
        c.TermsAndConditions = model.TermsAndConditions;
        c.Notes = model.Notes;
        c.TotalAmount = model.TotalAmount;
        c.PaidAmount = model.PaidAmount;
        c.StartDate = model.StartDate;
        c.EndDate = model.EndDate;
        c.SigningDate = model.SigningDate;
        c.RenewalDate = model.RenewalDate;
        c.ExpirationDate = model.ExpirationDate;
        c.Status = model.Status;
        c.ResponsibleUserId = model.ResponsibleUserId;
        c.ContractTemplateId = model.ContractTemplateId;
        c.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync(ct);
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
            .Include(c => c.ResponsibleUser)
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

        filter.Items = await query.OrderByDescending(c => c.ArchivedAt)
            .Select(c => new ContractIndexItemVM
            {
                Id = c.Id,
                ContractNumber = c.ContractNumber,
                Title = c.Title,
                ClientName = c.ClientName,
                CustomerName = c.Customer != null ? c.Customer.Name : null,
                TotalAmount = c.TotalAmount,
                PaidAmount = c.PaidAmount,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                ResponsibleUserName = c.ResponsibleUser != null ? c.ResponsibleUser.FullName : null,
                IsArchived = true,
                IsOwnedByCurrentUser = c.ResponsibleUserId == currentUserId || c.CreatedByUserId == currentUserId,
                CreatedAt = c.CreatedAt,
                ArchivedAt = c.ArchivedAt,
                ArchivedByUserName = c.ArchivedByUser != null ? c.ArchivedByUser.FullName : null
            }).ToListAsync(ct);
        filter.TotalCount = await _context.Contracts.AsNoTracking().CountAsync(c => c.IsArchived, ct);
        filter.FilteredCount = filter.Items.Count;
        filter.StatusOptions = ContractStatuses.GetStatusOptions();
        return filter;
    }

    public async Task<bool> ArchiveContractAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var c = await _context.Contracts.FindAsync(new object[] { id }, ct);
        if (c == null) return false;
        c.IsArchived = true; c.ArchivedAt = DateTime.Now; c.ArchivedByUserId = currentUserId;
        c.Status = ContractStatuses.Archived; c.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
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
            PaymentMethod = model.PaymentMethod?.Trim(),
            ReferenceNumber = model.ReferenceNumber?.Trim(),
            Description = model.Description?.Trim(),
            CreatedAt = DateTime.Now
        };
        _context.ContractPayments.Add(payment);
        contract.PaidAmount += model.Amount;
        contract.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Payment {Amount} added to contract {ContractId}", model.Amount, model.ContractId);
        return payment;
    }

    public async Task<bool> DeletePaymentAsync(int paymentId, int contractId, CancellationToken ct = default)
    {
        var payment = await _context.ContractPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.ContractId == contractId, ct);
        if (payment == null) return false;
        var contract = await _context.Contracts.FindAsync(new object[] { contractId }, ct);
        if (contract != null) { contract.PaidAmount -= payment.Amount; contract.UpdatedAt = DateTime.Now; }
        _context.ContractPayments.Remove(payment);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Attachments (upload / download)
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

        // Delete physical file
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
            .Where(t => t.UserId == currentUserId).OrderByDescending(t => t.IsDefault).ThenByDescending(t => t.UpdatedAt).ToListAsync(ct);
        var def = templates.FirstOrDefault(t => t.IsDefault);
        var setting = await _context.ContractUserSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == currentUserId, ct);
        var users = await _userManager.Users.OrderBy(u => u.FullName)
            .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u.Id, Text = u.FullName ?? u.UserName ?? u.Email ?? u.Id }).ToListAsync(ct);
        return new ContractSettingsVM
        {
            TemplateName = def?.Name ?? string.Empty,
            FormatJson = def?.FormatJson,
            TemplateHtml = def?.TemplateHtml,
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
                UpdatedAt = t.UpdatedAt
            }).ToList()
        };
    }

    public async Task<ContractTemplate> SaveTemplateAsync(ContractSettingsVM model, string currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.TemplateName)) throw new ArgumentException("Template name is required.");
        ContractTemplate template;
        if (model.CurrentTemplateId.HasValue)
        {
            template = await _context.ContractTemplates.FirstOrDefaultAsync(t => t.Id == model.CurrentTemplateId && t.UserId == currentUserId, ct)
                ?? throw new KeyNotFoundException("Template not found.");
        }
        else
        {
            template = new ContractTemplate { UserId = currentUserId, CreatedAt = DateTime.Now };
            _context.ContractTemplates.Add(template);
        }
        template.Name = model.TemplateName.Trim(); template.FormatJson = model.FormatJson;
        template.TemplateHtml = model.TemplateHtml; template.Description = model.Description?.Trim();
        template.UpdatedAt = DateTime.Now;
        if (model.IsDefault)
        {
            var defaults = await _context.ContractTemplates.Where(t => t.UserId == currentUserId && t.IsDefault && t.Id != template.Id).ToListAsync(ct);
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
        var t = await _context.ContractTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.UserId == currentUserId, ct);
        if (t == null) return false;
        var contracts = await _context.Contracts.Where(c => c.ContractTemplateId == templateId).ToListAsync(ct);
        foreach (var c in contracts) c.ContractTemplateId = null;
        _context.ContractTemplates.Remove(t);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetDefaultTemplateAsync(int templateId, string currentUserId, CancellationToken ct = default)
    {
        var t = await _context.ContractTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.UserId == currentUserId, ct);
        if (t == null) return false;
        var defaults = await _context.ContractTemplates.Where(x => x.UserId == currentUserId && x.IsDefault && x.Id != templateId).ToListAsync(ct);
        foreach (var d in defaults) d.IsDefault = false;
        t.IsDefault = true; t.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<ContractTemplate>> GetUserTemplatesAsync(string currentUserId, CancellationToken ct = default)
    {
        return await _context.ContractTemplates.AsNoTracking()
            .Where(t => t.UserId == currentUserId).OrderByDescending(t => t.IsDefault).ThenByDescending(t => t.UpdatedAt).ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Document Generation (template variable replacement)
    // ═══════════════════════════════════════════════════════════════

    public async Task<string> GenerateContractDocumentAsync(int contractId, int? templateId, string currentUserId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(currentUserId);
        IQueryable<Contract> query = _context.Contracts.AsNoTracking()
            .Include(c => c.Customer)
            .Include(c => c.ResponsibleUser)
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
                .Where(t => t.UserId == currentUserId && t.IsDefault)
                .FirstOrDefaultAsync(ct);

        var html = template?.TemplateHtml ?? GetDefaultTemplateHtml();

        // Replace dynamic variables
        var companyName = "شرکت راینو"; // Could be fetched from settings
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{contract_number}}"] = contract.ContractNumber,
            ["{{contract_title}}"] = contract.Title,
            ["{{contract_type}}"] = contract.ContractType ?? "-",
            ["{{customer_name}}"] = contract.Customer?.Name ?? contract.ClientName,
            ["{{customer_company}}"] = contract.Customer?.CompanyName ?? contract.ClientName,
            ["{{customer_phone}}"] = contract.Customer?.Phone ?? contract.ClientPhone ?? "-",
            ["{{customer_address}}"] = contract.Customer?.Address ?? contract.ClientAddress ?? "-",
            ["{{responsible_user}}"] = contract.ResponsibleUser?.FullName ?? contract.ResponsibleUser?.UserName ?? "-",
            ["{{start_date}}"] = contract.StartDate.ToString("yyyy/MM/dd"),
            ["{{end_date}}"] = contract.EndDate?.ToString("yyyy/MM/dd") ?? "-",
            ["{{signing_date}}"] = contract.SigningDate?.ToString("yyyy/MM/dd") ?? "-",
            ["{{expiration_date}}"] = contract.ExpirationDate?.ToString("yyyy/MM/dd") ?? "-",
            ["{{contract_amount}}"] = contract.TotalAmount.ToString("N0"),
            ["{{paid_amount}}"] = contract.PaidAmount.ToString("N0"),
            ["{{remaining_balance}}"] = contract.RemainingAmount.ToString("N0"),
            ["{{company_name}}"] = companyName,
            ["{{description}}"] = contract.Description ?? "",
            ["{{terms}}"] = contract.TermsAndConditions ?? "",
            ["{{status}}"] = contract.Status
        };

        foreach (var kvp in replacements)
        {
            html = html.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
        }

        return html;
    }

    private static string GetDefaultTemplateHtml()
    {
        return @"<!DOCTYPE html>
<html dir='rtl' lang='fa'>
<head><meta charset='utf-8'><title>قرارداد</title></head>
<body style='font-family: Vazirmatn, Tahoma, sans-serif; padding: 40px;'>
<h1 style='text-align:center;'>قرارداد</h1>
<p><strong>شماره:</strong> {{contract_number}}</p>
<p><strong>عنوان:</strong> {{contract_title}}</p>
<p><strong>نوع قرارداد:</strong> {{contract_type}}</p>
<hr/>
<h2>اطلاعات طرفین</h2>
<p><strong>مشتری:</strong> {{customer_name}}</p>
<p><strong>شرکت:</strong> {{customer_company}}</p>
<p><strong>تلفن:</strong> {{customer_phone}}</p>
<p><strong>آدرس:</strong> {{customer_address}}</p>
<hr/>
<h2>اطلاعات قرارداد</h2>
<p><strong>تاریخ شروع:</strong> {{start_date}}</p>
<p><strong>تاریخ پایان:</strong> {{end_date}}</p>
<p><strong>تاریخ امضا:</strong> {{signing_date}}</p>
<p><strong>تاریخ انقضا:</strong> {{expiration_date}}</p>
<p><strong>وضعیت:</strong> {{status}}</p>
<hr/>
<h2>اطلاعات مالی</h2>
<p><strong>مبلغ کل:</strong> {{contract_amount}} ریال</p>
<p><strong>مبلغ پرداخت شده:</strong> {{paid_amount}} ریال</p>
<p><strong>باقی مانده:</strong> {{remaining_balance}} ریال</p>
<hr/>
<h2>شرایط و ضوابط</h2>
<p>{{terms}}</p>
<hr/>
<h2>توضیحات</h2>
<p>{{description}}</p>
<hr/>
<p style='text-align:center;'><strong>{{company_name}}</strong></p>
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
        var expiringSoon = await query.CountAsync(c => c.Status == ContractStatuses.Active && !c.IsArchived
            && c.EndDate != null && c.EndDate >= now && c.EndDate <= thirtyDaysFromNow, ct);

        var totalAmount = await query.SumAsync(c => c.TotalAmount, ct);
        var totalPaid = await query.SumAsync(c => c.PaidAmount, ct);

        var fullyPaid = await query.CountAsync(c => c.PaidAmount >= c.TotalAmount && c.TotalAmount > 0, ct);
        var partiallyPaid = await query.CountAsync(c => c.PaidAmount > 0 && c.PaidAmount < c.TotalAmount, ct);
        var unpaid = await query.CountAsync(c => c.PaidAmount <= 0 && c.TotalAmount > 0, ct);

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
    //  Authorization
    // ═══════════════════════════════════════════════════════════════

    public bool CanUserViewContract(Contract contract, string currentUserId, bool isAdmin)
    {
        if (isAdmin) return true;
        if (contract.ResponsibleUserId == currentUserId) return true;
        if (contract.CreatedByUserId == currentUserId) return true;
        return false;
    }

    public bool CanUserEditContract(Contract contract, string currentUserId, bool isAdmin)
    {
        if (isAdmin) return true;
        if (contract.ResponsibleUserId == currentUserId) return true;
        if (contract.CreatedByUserId == currentUserId) return true;
        return false;
    }

    public bool CanUserViewFinancials(Contract contract, string currentUserId, bool isAdmin)
    {
        if (isAdmin) return true;
        if (contract.ResponsibleUserId == currentUserId) return true;
        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Validation
    // ═══════════════════════════════════════════════════════════════

    public async Task<bool> IsContractNumberUniqueAsync(string contractNumber, int? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.Contracts.AsNoTracking().Where(c => c.ContractNumber == contractNumber.Trim());
        if (excludeId.HasValue) query = query.Where(c => c.Id != excludeId.Value);
        return !await query.AnyAsync(ct);
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