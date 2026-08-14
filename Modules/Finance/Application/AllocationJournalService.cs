using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

public sealed record AllocationTemplateCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SourceSubsidiaryAccountId { get; set; }
    public decimal TotalAmount { get; set; }
    public int JournalTypeId { get; set; }
    public List<AllocationTemplateLineRequest> Lines { get; set; } = [];
}

public sealed record AllocationTemplateLineRequest
{
    public int TargetSubsidiaryAccountId { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? Ratio { get; set; }
    public string? Narration { get; set; }
}

/// <summary>
/// Manages allocation journal templates and executes allocation runs to generate
/// balanced accounting vouchers that distribute shared overhead costs across
/// multiple cost centers or accounts (Section 11 - Allocation Journals).
/// </summary>
public sealed class AllocationJournalService
{
    private readonly FinanceDbContext _context;

    public AllocationJournalService(FinanceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new allocation journal template with validated lines.
    /// </summary>
    public async Task<AllocationJournalTemplate> CreateTemplateAsync(
        AllocationTemplateCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Allocation template name is required.");
        }

        if (request.SourceSubsidiaryAccountId <= 0)
        {
            throw new InvalidOperationException("Source subsidiary account is required.");
        }

        if (request.TotalAmount <= 0)
        {
            throw new InvalidOperationException("Total allocation amount must be positive.");
        }

        var validLines = request.Lines
            .Where(item => item.TargetSubsidiaryAccountId > 0)
            .ToList();

        if (validLines.Count < 2)
        {
            throw new InvalidOperationException("An allocation template requires at least two target lines.");
        }

        ValidateAllocationLines(validLines);

        // Validate total percentages sum to 100 if using percentage mode
        if (validLines.Any(item => item.Percentage.HasValue))
        {
            var totalPercentage = validLines.Sum(item => item.Percentage!.Value);
            if (Math.Abs(totalPercentage - 100m) > 0.0001m)
            {
                throw new InvalidOperationException(
                    $"Total allocation percentages must equal 100%. Current total: {totalPercentage:N4}%.");
            }
        }

        var sourceAccount = await _context.SubsidiaryAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.SourceSubsidiaryAccountId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Source subsidiary account is invalid or inactive.");

        var journalType = await _context.JournalTypes
            .FirstOrDefaultAsync(item => item.Id == request.JournalTypeId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Journal type is invalid or inactive.");

        var template = new AllocationJournalTemplate
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            SourceSubsidiaryAccountId = sourceAccount.Id,
            TotalAmount = request.TotalAmount,
            JournalTypeId = journalType.Id,
            IsActive = true,
            Lines = validLines
                .Select((line, index) => new AllocationJournalTemplateLine
                {
                    TargetSubsidiaryAccountId = line.TargetSubsidiaryAccountId,
                    Percentage = line.Percentage,
                    Ratio = line.Ratio,
                    Narration = line.Narration,
                    DisplayOrder = index + 1
                })
                .ToList()
        };

        _context.AllocationJournalTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
        return template;
    }

    /// <summary>
    /// Executes an allocation run: generates a balanced Draft voucher from the template,
    /// distributing the TotalAmount across target accounts per the defined percentages/ratios.
    /// The voucher credits the source account and debits each target account.
    /// </summary>
    public async Task<VoucherHeader> ExecuteAllocationRunAsync(
        int templateId,
        DateTime runDate,
        bool postImmediately = false,
        string? issuedBy = null,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.AllocationJournalTemplates
            .Include(item => item.JournalType)
            .Include(item => item.Lines)
            .ThenInclude(line => line.TargetSubsidiaryAccount)
            .FirstOrDefaultAsync(item => item.Id == templateId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Allocation template '{templateId}' was not found or is inactive.");

        var sourceAccount = await _context.SubsidiaryAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == template.SourceSubsidiaryAccountId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Source subsidiary account is no longer active.");

        var fiscalYear = await _context.FiscalYears
            .Where(item => item.StartDate <= runDate.Date && item.EndDate >= runDate.Date && !item.IsClosed)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("سال مالی باز برای تاریخ اجرای تخصیص یافت نشد.");

        var allocations = ComputeAllocations(template);

        var displayOrder = 1;
        var lines = new List<VoucherLine>();

        // Credit the source account for the total amount
        lines.Add(new VoucherLine
        {
            SubsidiaryAccountId = sourceAccount.Id,
            DebitAmount = 0,
            CreditAmount = template.TotalAmount,
            ExchangeRate = 1m,
            Narration = $"Allocation source: {template.Name}",
            DisplayOrder = displayOrder++
        });

        // Debit each target account
        foreach (var allocation in allocations)
        {
            lines.Add(new VoucherLine
            {
                SubsidiaryAccountId = allocation.TargetSubsidiaryAccountId,
                DebitAmount = allocation.Amount,
                CreditAmount = 0,
                ExchangeRate = 1m,
                Narration = allocation.Narration ?? $"Allocation to target from {template.Name}",
                DisplayOrder = displayOrder++
            });
        }

        var totalDebits = lines.Sum(item => item.DebitAmount);
        var totalCredits = lines.Sum(item => item.CreditAmount);
        if (totalDebits != totalCredits)
        {
            throw new InvalidOperationException(
                $"Allocation voucher is not balanced. Debits={totalDebits:N2}, Credits={totalCredits:N2}.");
        }

        var voucher = new VoucherHeader
        {
            DocumentNumber = $"ALLOC-{template.Id}-{runDate:yyyyMMdd}-{Guid.NewGuid():N}"[..32],
            VoucherDate = runDate.Date,
            Description = $"Allocation journal: {template.Name} — {template.Description}",
            Status = postImmediately ? VoucherStatus.Permanent : VoucherStatus.Draft,
            PostingStatus = postImmediately ? PostingStatus.Posted : PostingStatus.Draft,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = template.JournalTypeId,
            IssuedBy = issuedBy,
            IssuedAt = DateTimeOffset.UtcNow,
            Lines = lines
        };

        if (postImmediately)
        {
            var validationGuard = new VoucherValidationGuard(_context);
            await validationGuard.ValidateAsync(voucher, cancellationToken);
        }

        voucher.TotalDebits = totalDebits;
        voucher.TotalCredits = totalCredits;

        _context.VoucherHeaders.Add(voucher);
        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    /// <summary>
    /// Lists all active allocation templates.
    /// </summary>
    public async Task<List<AllocationJournalTemplate>> ListTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.AllocationJournalTemplates
            .AsNoTracking()
            .Include(item => item.SourceSubsidiaryAccount)
            .Include(item => item.JournalType)
            .Include(item => item.Lines)
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes (deactivates) an allocation template.
    /// </summary>
    public async Task DeactivateTemplateAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.AllocationJournalTemplates
            .FirstOrDefaultAsync(item => item.Id == templateId, cancellationToken)
            ?? throw new InvalidOperationException($"Allocation template '{templateId}' was not found.");

        template.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public static List<AllocationResult> ComputeAllocations(AllocationJournalTemplate template)
    {
        var results = new List<AllocationResult>();
        var lines = template.Lines.OrderBy(item => item.DisplayOrder).ToList();

        bool hasPercentages = lines.Any(item => item.Percentage.HasValue);
        bool hasRatios = lines.Any(item => item.Ratio.HasValue);

        if (hasPercentages && hasRatios)
        {
            throw new InvalidOperationException("Cannot mix Percentage and Ratio allocation methods in the same template.");
        }

        if (hasPercentages)
        {
            var totalPercentage = lines.Sum(item => item.Percentage!.Value);
            if (Math.Abs(totalPercentage - 100m) > 0.0001m)
            {
                throw new InvalidOperationException(
                    $"Total allocation percentages must equal 100%. Current total: {totalPercentage:N4}%.");
            }

            foreach (var line in lines)
            {
                var amount = Math.Round(template.TotalAmount * line.Percentage!.Value / 100m, 2, MidpointRounding.AwayFromZero);
                results.Add(new AllocationResult(line.TargetSubsidiaryAccountId, amount, line.Narration));
            }
        }
        else if (hasRatios)
        {
            var totalRatio = lines.Sum(item => item.Ratio!.Value);
            if (totalRatio <= 0)
            {
                throw new InvalidOperationException("Total allocation ratios must be positive.");
            }

            foreach (var line in lines)
            {
                var amount = Math.Round(template.TotalAmount * line.Ratio!.Value / totalRatio, 2, MidpointRounding.AwayFromZero);
                results.Add(new AllocationResult(line.TargetSubsidiaryAccountId, amount, line.Narration));
            }
        }
        else
        {
            throw new InvalidOperationException("Each allocation line must define either a Percentage or a Ratio.");
        }

        // Handle rounding difference: adjust the largest line
        var computedTotal = results.Sum(item => item.Amount);
        var difference = template.TotalAmount - computedTotal;
        if (difference != 0 && results.Count > 0)
        {
            var largest = results.OrderByDescending(item => item.Amount).First();
            results.Remove(largest);
            results.Add(new AllocationResult(largest.TargetSubsidiaryAccountId, largest.Amount + difference, largest.Narration));
        }

        return results;
    }

    private static void ValidateAllocationLines(List<AllocationTemplateLineRequest> lines)
    {
        bool anyPercentage = lines.Any(item => item.Percentage.HasValue);
        bool anyRatio = lines.Any(item => item.Ratio.HasValue);

        if (anyPercentage && anyRatio)
        {
            throw new InvalidOperationException("Cannot mix Percentage and Ratio allocation methods in the same template.");
        }

        foreach (var line in lines)
        {
            if (line.Percentage.HasValue && line.Percentage.Value is < 0 or > 100)
            {
                throw new InvalidOperationException($"Percentage must be between 0 and 100. Got {line.Percentage.Value}.");
            }

            if (line.Ratio.HasValue && line.Ratio.Value <= 0)
            {
                throw new InvalidOperationException($"Ratio must be positive. Got {line.Ratio.Value}.");
            }
        }
    }

    public sealed record AllocationResult(
        int TargetSubsidiaryAccountId,
        decimal Amount,
        string? Narration);
}