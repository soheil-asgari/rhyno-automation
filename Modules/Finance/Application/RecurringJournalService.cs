using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

public sealed record TemplateCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int JournalTypeId { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurrenceEveryMonths { get; set; }
    public DateTime? NextRunDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Reference { get; set; }
    public List<TemplateLineRequest> Lines { get; set; } = [];
}

public sealed record TemplateLineRequest
{
    public int SubsidiaryAccountId { get; set; }
    public Guid? FloatingDetailAccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Narration { get; set; }
}

/// <summary>
/// Creates and runs recurring / template journals (Section 11). Templates generate actual,
/// valid, balanced accounting vouchers preserving the double-entry invariant.
/// </summary>
public sealed class RecurringJournalService
{
    private readonly FinanceDbContext _context;
    private readonly FinanceLedgerService _ledgerService;

    public RecurringJournalService(FinanceDbContext context)
    {
        _context = context;
        _ledgerService = new FinanceLedgerService(context);
    }

    public async Task<JournalTemplate> CreateTemplateAsync(
        TemplateCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Journal template name is required.");
        }

        var lines = request.Lines
            .Where(item => item.SubsidiaryAccountId > 0 && (item.DebitAmount > 0 || item.CreditAmount > 0))
            .ToList();
        if (lines.Count < 2)
        {
            throw new InvalidOperationException("A journal template requires at least two valid lines.");
        }

        if (lines.Sum(item => item.DebitAmount) != lines.Sum(item => item.CreditAmount))
        {
            throw new InvalidOperationException("A journal template must be balanced (total debit = total credit).");
        }

        var journalType = await _context.JournalTypes
            .FirstOrDefaultAsync(item => item.Id == request.JournalTypeId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Journal type is invalid or inactive.");

        var template = new JournalTemplate
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            JournalTypeId = journalType.Id,
            IsRecurring = request.IsRecurring,
            RecurrenceEveryMonths = request.RecurrenceEveryMonths,
            NextRunDate = request.NextRunDate,
            IsActive = request.IsActive,
            Reference = request.Reference,
            Lines = lines
                .Select((line, index) => new JournalTemplateLine
                {
                    SubsidiaryAccountId = line.SubsidiaryAccountId,
                    FloatingDetailAccountId = line.FloatingDetailAccountId,
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount,
                    Narration = line.Narration,
                    DisplayOrder = index + 1
                })
                .ToList()
        };

        _context.JournalTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
        return template;
    }

    /// <summary>
    /// Generates a balanced voucher from a template for the given date. The result is a
    /// Draft voucher that the caller can review and post; it never affects the template.
    /// </summary>
    public async Task<VoucherHeader> RunTemplateAsync(
        int templateId,
        DateTime runDate,
        string? issuedBy = null,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.JournalTemplates
            .Include(item => item.JournalType)
            .Include(item => item.Lines)
            .ThenInclude(line => line.SubsidiaryAccount)
            .FirstOrDefaultAsync(item => item.Id == templateId, cancellationToken)
            ?? throw new InvalidOperationException($"Journal template '{templateId}' was not found.");

        if (!template.IsActive)
        {
            throw new InvalidOperationException($"Journal template '{template.Name}' is inactive.");
        }

        var fiscalYear = await _context.FiscalYears
            .Where(item => item.StartDate <= runDate.Date && item.EndDate >= runDate.Date && !item.IsClosed)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("سال مالی باز برای تاریخ سند یافت نشد.");

        var voucher = new VoucherHeader
        {
            DocumentNumber = $"TPL-{template.Id}-{runDate:yyyyMMdd}-{Guid.NewGuid():N}"[..32],
            VoucherDate = runDate.Date,
            Description = $"{template.Name}: {template.Description}",
            Reference = template.Reference,
            Status = VoucherStatus.Draft,
            PostingStatus = PostingStatus.Draft,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = template.JournalTypeId,
            IssuedBy = issuedBy,
            IssuedAt = DateTimeOffset.UtcNow,
            Lines = template.Lines
                .OrderBy(item => item.DisplayOrder)
                .Select(item => new VoucherLine
                {
                    SubsidiaryAccountId = item.SubsidiaryAccountId,
                    FloatingDetailAccountId = item.FloatingDetailAccountId,
                    DebitAmount = item.DebitAmount,
                    CreditAmount = item.CreditAmount,
                    ExchangeRate = 1m,
                    Narration = item.Narration,
                    DisplayOrder = item.DisplayOrder
                })
                .ToList()
        };

        var validationGuard = new VoucherValidationGuard(_context);
        await validationGuard.ValidateAsync(voucher, cancellationToken);

        voucher.TotalDebits = voucher.Lines.Sum(line => line.DebitAmount);
        voucher.TotalCredits = voucher.Lines.Sum(line => line.CreditAmount);
        if (voucher.TotalDebits != voucher.TotalCredits)
        {
            throw new InvalidOperationException("Template-generated voucher is not balanced.");
        }

        _context.VoucherHeaders.Add(voucher);
        await _context.SaveChangesAsync(cancellationToken);

        // Advance the next run date for recurring templates.
        if (template.IsRecurring && template.RecurrenceEveryMonths.HasValue)
        {
            template.NextRunDate = runDate.Date.AddMonths(template.RecurrenceEveryMonths.Value);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return voucher;
    }

    /// <summary>
    /// Generates a template voucher, then immediately posts it through the shared posting pipeline.
    /// </summary>
    public async Task<VoucherHeader> RunAndPostAsync(
        int templateId,
        DateTime runDate,
        string? issuedBy = null,
        CancellationToken cancellationToken = default)
    {
        var draft = await RunTemplateAsync(templateId, runDate, issuedBy, cancellationToken);
        return await _ledgerService.PostVoucherAsync(draft.Id, issuedBy, cancellationToken);
    }
}