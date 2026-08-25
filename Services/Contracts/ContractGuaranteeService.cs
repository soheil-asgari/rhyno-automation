using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public sealed class ContractGuaranteeService : IContractGuaranteeService
{
    private readonly OfficeDbContext _context;
    private readonly ILogger<ContractGuaranteeService> _logger;

    public ContractGuaranteeService(
        OfficeDbContext context,
        ILogger<ContractGuaranteeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ContractGuaranteeVM>> GetGuaranteesAsync(int contractId, CancellationToken ct = default)
    {
        return await _context.ContractGuarantees
            .AsNoTracking()
            .Where(g => g.ContractId == contractId)
            .Select(g => new ContractGuaranteeVM
            {
                Id = g.Id,
                ContractId = g.ContractId,
                AmendmentId = g.AmendmentId,
                GuaranteeType = g.GuaranteeType,
                GuaranteeNumber = g.GuaranteeNumber,
                IssuingBank = g.IssuingBank,
                Amount = g.Amount,
                IssuedDate = g.IssuedDate,
                ExpiryDate = g.ExpiryDate,
                Status = g.Status,
                RetentionPercentage = g.RetentionPercentage,
                ReleaseCondition = g.ReleaseCondition,
                ReleasedDate = g.ReleasedDate,
                CreatedByUserName = g.CreatedByUser != null ? g.CreatedByUser.FullName ?? g.CreatedByUser.UserName ?? "" : "",
                CreatedAt = g.CreatedAt
            })
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);
    }
public async Task<ContractGuarantee> AddGuaranteeAsync(ContractGuaranteeCreateVM model, string currentUserId, CancellationToken ct = default)
    {
        var entity = new ContractGuarantee
        {
            ContractId = model.ContractId,
            AmendmentId = model.AmendmentId,
            GuaranteeType = model.GuaranteeType,
            GuaranteeNumber = model.GuaranteeNumber,
            IssuingBank = model.IssuingBank,
            Amount = model.Amount,
            IssuedDate = model.IssuedDate,
            ExpiryDate = model.ExpiryDate,
            Status = GuaranteeStatusNames.Active,
            RetentionPercentage = model.GuaranteeType == GuaranteeTypeNames.RetentionMoney ? model.RetentionPercentage : null,
            ReleaseCondition = model.ReleaseCondition,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContractGuarantees.Add(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Guarantee {GuaranteeNumber} added for contract {ContractId} by {UserId}",
            model.GuaranteeNumber, model.ContractId, currentUserId);

        return entity;
    }

    public async Task<ContractGuarantee> UpdateGuaranteeAsync(ContractGuaranteeEditVM model, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _context.ContractGuarantees
            .FirstOrDefaultAsync(g => g.Id == model.Id && g.ContractId == model.ContractId, ct);

        if (entity == null)
            throw new InvalidOperationException("ضمانت مورد نظر یافت نشد.");

        if (entity.Status is GuaranteeStatusNames.Released or GuaranteeStatusNames.Claimed or GuaranteeStatusNames.Expired)
            throw new InvalidOperationException("ضمانت‌نامه در وضعیت قابل ویرایش نیست.");

        entity.GuaranteeType = model.GuaranteeType;
        entity.GuaranteeNumber = model.GuaranteeNumber;
        entity.IssuingBank = model.IssuingBank;
        entity.Amount = model.Amount;
        entity.IssuedDate = model.IssuedDate;
        entity.ExpiryDate = model.ExpiryDate;
        entity.RetentionPercentage = model.GuaranteeType == GuaranteeTypeNames.RetentionMoney ? model.RetentionPercentage : null;
        entity.ReleaseCondition = model.ReleaseCondition;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Guarantee {Id} updated by {UserId}", model.Id, currentUserId);
        return entity;
    }

    public async Task<bool> ReleaseGuaranteeAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _context.ContractGuarantees
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (entity == null) return false;

        if (entity.Status != GuaranteeStatusNames.Active)
            throw new InvalidOperationException("فقط ضمانت‌نامه‌های فعال قابل آزادسازی هستند.");

        if (DateTime.Today < entity.ExpiryDate)
            throw new InvalidOperationException("تاریخ انقضای ضمانت‌نامه فرا نرسیده است.");

        entity.Status = GuaranteeStatusNames.Released;
        entity.ReleasedDate = DateTime.Today;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Guarantee {Id} released by {UserId}", id, currentUserId);
        return true;
    }

    public async Task<bool> DeleteGuaranteeAsync(int id, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _context.ContractGuarantees
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (entity == null) return false;

        _context.ContractGuarantees.Remove(entity);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Guarantee {Id} deleted by {UserId}", id, currentUserId);
        return true;
    }

    public async Task<List<ContractGuarantee>> GetExpiringGuaranteesAsync(CancellationToken ct = default)
    {
        var threshold = DateTime.Today.AddDays(30);
        return await _context.ContractGuarantees
            .AsNoTracking()
            .Include(g => g.Contract)
            .Where(g => g.Status == GuaranteeStatusNames.Active
                     && g.ExpiryDate <= threshold
                     && g.ExpiryDate >= DateTime.Today)
            .ToListAsync(ct);
    }

    public async Task<int> UpdateExpiringGuaranteeStatusesAsync(CancellationToken ct = default)
    {
        var now = DateTime.Today;
        var threshold = now.AddDays(30);
        var guarantees = await _context.ContractGuarantees
            .Where(g => g.Status == GuaranteeStatusNames.Active || g.Status == GuaranteeStatusNames.Expiring)
            .ToListAsync(ct);
        int updated = 0;
        foreach (var g in guarantees)
        {
            if (g.Status == GuaranteeStatusNames.Active && g.ExpiryDate.Date <= threshold && g.ExpiryDate.Date >= now)
            {
                g.Status = GuaranteeStatusNames.Expiring; g.UpdatedAt = DateTime.UtcNow; updated++;
            }
            else if (g.Status is GuaranteeStatusNames.Active or GuaranteeStatusNames.Expiring && g.ExpiryDate.Date < now)
            {
                g.Status = GuaranteeStatusNames.Expired; g.UpdatedAt = DateTime.UtcNow; updated++;
            }
        }
        if (updated > 0) await _context.SaveChangesAsync(ct);
        return updated;
    }
}