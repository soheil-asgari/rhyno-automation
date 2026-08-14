using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Filters;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Controllers.Api.v1;

[Authorize]
[RequireAccessArea("Finance")]
[Route("api/v1/finance/chart-of-accounts")]
[ApiController]
public sealed class ChartOfAccountsApiController : ControllerBase
{
    private readonly FinanceDbContext _context;

    public ChartOfAccountsApiController(FinanceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the full account hierarchy as a tree structure.
    /// </summary>
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree([FromQuery] string? search, CancellationToken ct)
    {
        var query = _context.AccountGroups
            .AsNoTracking()
            .Include(g => g.GeneralAccounts)
                .ThenInclude(ga => ga.SubsidiaryAccounts)
                    .ThenInclude(sa => sa.DetailedAccounts)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(g =>
                g.Code.ToLower().Contains(term) ||
                g.Name.ToLower().Contains(term) ||
                g.GeneralAccounts.Any(ga =>
                    ga.Code.ToLower().Contains(term) ||
                    ga.Name.ToLower().Contains(term) ||
                    ga.SubsidiaryAccounts.Any(sa =>
                        sa.Code.ToLower().Contains(term) ||
                        sa.Name.ToLower().Contains(term) ||
                        sa.DetailedAccounts.Any(da =>
                            da.Code.ToLower().Contains(term) ||
                            da.Name.ToLower().Contains(term)))));
        }

        var groups = await query.OrderBy(g => g.Code).ToListAsync(ct);

        var tree = groups.Select(g => new AccountNode
        {
            Id = g.Id,
            Code = g.Code,
            Name = g.Name,
            Nature = g.Nature.ToString(),
            IsActive = g.IsActive,
            Level = "Group",
            Children = g.GeneralAccounts.OrderBy(ga => ga.Code).Select(ga => new AccountNode
            {
                Id = ga.Id,
                Code = ga.Code,
                Name = ga.Name,
                Nature = ga.Nature.ToString(),
                IsActive = ga.IsActive,
                Level = "General",
                Children = ga.SubsidiaryAccounts.OrderBy(sa => sa.Code).Select(sa => new AccountNode
                {
                    Id = sa.Id,
                    Code = sa.Code,
                    Name = sa.Name,
                    Nature = sa.Nature.ToString(),
                    IsActive = sa.IsActive,
                    Level = "Subsidiary",
                    SystemKey = sa.SystemKey,
                    IsTemporary = sa.IsTemporary,
                    AllowsFloatingDetail = sa.AllowsFloatingDetail,
                    Children = sa.DetailedAccounts.OrderBy(da => da.Code).Select(da => new AccountNode
                    {
                        Id = da.Id,
                        Code = da.Code,
                        Name = da.Name,
                        IsActive = da.IsActive,
                        Level = "Detailed",
                        IsFloating = da.IsFloating,
                        Children = []
                    }).ToList()
                }).ToList()
            }).ToList()
        }).ToList();

        return Ok(tree);
    }

    /// <summary>
    /// Returns a flat list of accounts for dropdowns and search.
    /// </summary>
    [HttpGet("flat")]
    public async Task<IActionResult> GetFlat([FromQuery] string? search, [FromQuery] bool? active, CancellationToken ct)
    {
        var query = _context.SubsidiaryAccounts
            .AsNoTracking()
            .Include(sa => sa.GeneralAccount)
                .ThenInclude(ga => ga.AccountGroup)
            .AsQueryable();

        if (active.HasValue)
        {
            query = query.Where(sa => sa.IsActive == active.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(sa =>
                sa.Code.ToLower().Contains(term) ||
                sa.Name.ToLower().Contains(term) ||
                sa.GeneralAccount.Name.ToLower().Contains(term) ||
                sa.GeneralAccount.AccountGroup.Name.ToLower().Contains(term));
        }

        var accounts = await query
            .OrderBy(sa => sa.GeneralAccount.AccountGroup.Code)
            .ThenBy(sa => sa.GeneralAccount.Code)
            .ThenBy(sa => sa.Code)
            .Select(sa => new
            {
                id = sa.Id,
                code = sa.Code,
                name = sa.Name,
                nature = sa.Nature.ToString(),
                generalAccount = sa.GeneralAccount.Name,
                accountGroup = sa.GeneralAccount.AccountGroup.Name,
                isActive = sa.IsActive,
                isTemporary = sa.IsTemporary,
                systemKey = sa.SystemKey
            })
            .ToListAsync(ct);

        return Ok(accounts);
    }

    /// <summary>
    /// Creates a new account at any hierarchy level.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "کد و نام حساب الزامی است." });
        }

        switch (request.Level?.ToLower())
        {
            case "group":
                return await CreateAccountGroupAsync(request, ct);
            case "general":
                return await CreateGeneralAccountAsync(request, ct);
            case "subsidiary":
                return await CreateSubsidiaryAccountAsync(request, ct);
            case "detailed":
                return await CreateDetailedAccountAsync(request, ct);
            default:
                return BadRequest(new { success = false, message = "سطح حساب نامعتبر است." });
        }
    }

    /// <summary>
    /// Updates an existing account.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAccountRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "نام حساب الزامی است." });
        }

        // Try to find at each level
        var group = await _context.AccountGroups.FindAsync(id);
        if (group != null)
        {
            if (await IsDuplicateCodeAsync(request.Code!, "group", id, ct))
                return BadRequest(new { success = false, message = "کد حساب تکراری است." });
            group.Code = request.Code ?? group.Code;
            group.Name = request.Name;
            group.IsActive = request.IsActive ?? group.IsActive;
            if (Enum.TryParse<AccountNature>(request.Nature, true, out var nature))
                group.Nature = nature;
            await _context.SaveChangesAsync(ct);
            return Ok(new { success = true, message = "حساب با موفقیت به‌روزرسانی شد." });
        }

        var general = await _context.GeneralAccounts.FindAsync(id);
        if (general != null)
        {
            if (await IsDuplicateCodeAsync(request.Code!, "general", id, ct))
                return BadRequest(new { success = false, message = "کد حساب تکراری است." });
            general.Code = request.Code ?? general.Code;
            general.Name = request.Name;
            general.IsActive = request.IsActive ?? general.IsActive;
            if (Enum.TryParse<AccountNature>(request.Nature, true, out var nature))
                general.Nature = nature;
            await _context.SaveChangesAsync(ct);
            return Ok(new { success = true, message = "حساب با موفقیت به‌روزرسانی شد." });
        }

        var subsidiary = await _context.SubsidiaryAccounts.FindAsync(id);
        if (subsidiary != null)
        {
            if (await IsDuplicateCodeAsync(request.Code!, "subsidiary", id, ct))
                return BadRequest(new { success = false, message = "کد حساب تکراری است." });
            subsidiary.Code = request.Code ?? subsidiary.Code;
            subsidiary.Name = request.Name;
            subsidiary.IsActive = request.IsActive ?? subsidiary.IsActive;
            if (Enum.TryParse<AccountNature>(request.Nature, true, out var nature))
                subsidiary.Nature = nature;
            // Prevent marking subsidiary as postable if it has children
            if (request.IsTemporary.HasValue)
                subsidiary.IsTemporary = request.IsTemporary.Value;
            if (request.AllowsFloatingDetail.HasValue)
                subsidiary.AllowsFloatingDetail = request.AllowsFloatingDetail.Value;
            await _context.SaveChangesAsync(ct);
            return Ok(new { success = true, message = "حساب با موفقیت به‌روزرسانی شد." });
        }

        var detailed = await _context.DetailedAccounts.FindAsync(id);
        if (detailed != null)
        {
            detailed.Name = request.Name;
            detailed.IsActive = request.IsActive ?? detailed.IsActive;
            await _context.SaveChangesAsync(ct);
            return Ok(new { success = true, message = "حساب با موفقیت به‌روزرسانی شد." });
        }

        return NotFound(new { success = false, message = "حساب یافت نشد." });
    }

    /// <summary>
    /// Returns recent voucher activity for a subsidiary account.
    /// </summary>
    [HttpGet("activity/{subsidiaryAccountId}")]
    public async Task<IActionResult> GetActivity(int subsidiaryAccountId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var activities = await _context.VoucherLines
            .AsNoTracking()
            .Where(line => line.SubsidiaryAccountId == subsidiaryAccountId)
            .Include(line => line.VoucherHeader)
            .OrderByDescending(line => line.VoucherHeader.VoucherDate)
            .Take(limit)
            .Select(line => new
            {
                voucherId = line.VoucherHeaderId,
                date = line.VoucherHeader.VoucherDate,
                description = line.VoucherHeader.Description,
                debit = line.DebitAmount,
                credit = line.CreditAmount,
                status = line.VoucherHeader.PostingStatus,
                narration = line.Narration
            })
            .ToListAsync(ct);

        return Ok(activities);
    }

    /// <summary>
    /// Validates that an account code is unique within its hierarchy level.
    /// </summary>
    [HttpGet("validate-code")]
    public async Task<IActionResult> ValidateCode(
        [FromQuery] string code,
        [FromQuery] string level,
        [FromQuery] int? excludeId,
        CancellationToken ct)
    {
        var isDuplicate = await IsDuplicateCodeAsync(code, level, excludeId, ct);
        return Ok(new { isDuplicate, message = isDuplicate ? "کد حساب تکراری است." : "کد معتبر است." });
    }

    private async Task<bool> IsDuplicateCodeAsync(string code, string level, int? excludeId, CancellationToken ct)
    {
        return level.ToLower() switch
        {
            "group" => await _context.AccountGroups.AnyAsync(g => g.Code == code && (!excludeId.HasValue || g.Id != excludeId.Value), ct),
            "general" => await _context.GeneralAccounts.AnyAsync(g => g.Code == code && (!excludeId.HasValue || g.Id != excludeId.Value), ct),
            "subsidiary" => await _context.SubsidiaryAccounts.AnyAsync(s => s.Code == code && (!excludeId.HasValue || s.Id != excludeId.Value), ct),
            "detailed" => await _context.DetailedAccounts.AnyAsync(d => d.Code == code && (!excludeId.HasValue || d.Id != excludeId.Value), ct),
            _ => false
        };
    }

    private async Task<IActionResult> CreateAccountGroupAsync(CreateAccountRequest request, CancellationToken ct)
    {
        if (await IsDuplicateCodeAsync(request.Code, "group", null, ct))
            return BadRequest(new { success = false, message = "کد حساب تکراری است." });

        var group = new AccountGroup
        {
            Code = request.Code,
            Name = request.Name,
            Nature = Enum.TryParse<AccountNature>(request.Nature, true, out var n) ? n : AccountNature.NoControl,
            IsActive = true
        };
        _context.AccountGroups.Add(group);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "گروه حساب ایجاد شد.", id = group.Id });
    }

    private async Task<IActionResult> CreateGeneralAccountAsync(CreateAccountRequest request, CancellationToken ct)
    {
        if (request.ParentId <= 0)
            return BadRequest(new { success = false, message = "گروه حساب والد الزامی است." });
        if (await IsDuplicateCodeAsync(request.Code, "general", null, ct))
            return BadRequest(new { success = false, message = "کد حساب تکراری است." });

        var parent = await _context.AccountGroups.FindAsync(request.ParentId);
        if (parent == null)
            return BadRequest(new { success = false, message = "گروه حساب والد یافت نشد." });

        var account = new GeneralAccount
        {
            AccountGroupId = parent.Id,
            Code = request.Code,
            Name = request.Name,
            Nature = Enum.TryParse<AccountNature>(request.Nature, true, out var n) ? n : AccountNature.NoControl,
            IsActive = true
        };
        _context.GeneralAccounts.Add(account);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "حساب کل ایجاد شد.", id = account.Id });
    }

    private async Task<IActionResult> CreateSubsidiaryAccountAsync(CreateAccountRequest request, CancellationToken ct)
    {
        if (request.ParentId <= 0)
            return BadRequest(new { success = false, message = "حساب کل والد الزامی است." });
        if (await IsDuplicateCodeAsync(request.Code, "subsidiary", null, ct))
            return BadRequest(new { success = false, message = "کد حساب تکراری است." });

        var parent = await _context.GeneralAccounts.FindAsync(request.ParentId);
        if (parent == null)
            return BadRequest(new { success = false, message = "حساب کل والد یافت نشد." });

        var account = new SubsidiaryAccount
        {
            GeneralAccountId = parent.Id,
            Code = request.Code,
            Name = request.Name,
            Nature = Enum.TryParse<AccountNature>(request.Nature, true, out var n) ? n : AccountNature.NoControl,
            IsActive = true,
            IsTemporary = request.IsTemporary ?? false,
            AllowsFloatingDetail = request.AllowsFloatingDetail ?? true,
            SystemKey = request.SystemKey ?? string.Empty
        };
        _context.SubsidiaryAccounts.Add(account);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "حساب معین ایجاد شد.", id = account.Id });
    }

    private async Task<IActionResult> CreateDetailedAccountAsync(CreateAccountRequest request, CancellationToken ct)
    {
        if (request.ParentId <= 0)
            return BadRequest(new { success = false, message = "حساب معین والد الزامی است." });
        if (await IsDuplicateCodeAsync(request.Code, "detailed", null, ct))
            return BadRequest(new { success = false, message = "کد حساب تکراری است." });

        var parent = await _context.SubsidiaryAccounts.FindAsync(request.ParentId);
        if (parent == null)
            return BadRequest(new { success = false, message = "حساب معین والد یافت نشد." });

        // Prevent adding detailed accounts to non-postable parents
        if (parent.IsTemporary)
            return BadRequest(new { success = false, message = "حساب‌های موقت قابلیت تفصیل ندارند." });

        var account = new DetailedAccount
        {
            SubsidiaryAccountId = parent.Id,
            Code = request.Code,
            Name = request.Name,
            IsActive = true,
            IsFloating = true
        };
        _context.DetailedAccounts.Add(account);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "حساب تفصیلی ایجاد شد.", id = account.Id });
    }
}

public sealed class AccountNode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nature { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Level { get; set; } = string.Empty;
    public string? SystemKey { get; set; }
    public bool? IsTemporary { get; set; }
    public bool? AllowsFloatingDetail { get; set; }
    public bool? IsFloating { get; set; }
    public List<AccountNode> Children { get; set; } = [];
}

public sealed class CreateAccountRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Nature { get; set; }
    public string? Level { get; set; }
    public int? ParentId { get; set; }
    public string? SystemKey { get; set; }
    public bool? IsTemporary { get; set; }
    public bool? AllowsFloatingDetail { get; set; }
}

public sealed class UpdateAccountRequest
{
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Nature { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsTemporary { get; set; }
    public bool? AllowsFloatingDetail { get; set; }
}