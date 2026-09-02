using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Financial;

/// <summary>
/// Parses hesab.txt, preserves original codes, and seeds
/// AccountGroup / GeneralLedger / SubsidiaryLedger tables idempotently.
/// Detects existing records by their original business code (not auto-generated).
/// Clears any dummy seed data before importing.
/// </summary>
public sealed class ChartOfAccountsSeeder
{
    private readonly FinanceDbContext _db;
    private readonly ILogger<ChartOfAccountsSeeder> _logger;

    private sealed record RawRow(
        string SrcGroupCode,   // col[0]: e.g. "11"
        string GroupTitle,     // col[1]: e.g. "داراییهای جاری"
        string SrcGlCode,      // col[2]: e.g. "1110"
        string GlTitle,        // col[3]: e.g. "موجودی نقد وبانک"
        string SrcSlCode,      // col[4]: e.g. "111001"
        string SlTitle,        // col[5]: e.g. "اسناد خزانه اسلامی"
        string SrcFullCode);   // col[6]: e.g. "111110111001"

    public ChartOfAccountsSeeder(FinanceDbContext db, ILogger<ChartOfAccountsSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(string hesabFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(hesabFilePath))
            throw new FileNotFoundException("hesab.txt not found", hesabFilePath);

        var lines = await File.ReadAllLinesAsync(hesabFilePath, ct);
        var rows = new List<RawRow>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('\t');
            if (parts.Length < 7) continue;
            rows.Add(new RawRow(
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim(),
                parts[3].Trim(),
                parts[4].Trim(),
                parts[5].Trim(),
                parts[6].Trim()));
        }

        if (rows.Count == 0)
        {
            _logger.LogWarning("hesab.txt is empty — no accounts to seed.");
            return;
        }

        // ── 1. Clear dummy seed data if present (codes that don't match hesab.txt pattern) ──
        await ClearDummySeedDataAsync(rows, ct);

        // ── 2. Build distinct groups from hesab.txt using source codes ──
        // Group code is the first 2 chars of the full code (e.g. "11" from "111110111001")
        var distinctGroups = rows
            .Select(r => (Code: r.SrcGroupCode, r.GroupTitle))
            .Distinct()
            .OrderBy(g => g.Code)
            .ToList();

        var groupCodeToId = new Dictionary<string, int>();

        foreach (var (code, title) in distinctGroups)
        {
            var existing = await _db.AccountGroups
                .FirstOrDefaultAsync(g => g.Code == code, ct);

            if (existing != null)
            {
                groupCodeToId[code] = existing.Id;
                continue;
            }

            var entity = new AccountGroup
            {
                Code = code,
                Name = title,
                IsActive = true,
            };
            _db.AccountGroups.Add(entity);
            await _db.SaveChangesAsync(ct);
            groupCodeToId[code] = entity.Id;
        }
        _logger.LogInformation("Seeded {Count} AccountGroups.", distinctGroups.Count);

        // ── 3. Build distinct GLs ──
        // GL code is the first 4 chars of the full code (e.g. "1111" from "111110111001")
        var distinctGl = rows
            .Select(r => (Code: r.SrcGlCode, r.GlTitle, GroupCode: r.SrcGroupCode))
            .Distinct()
            .OrderBy(g => g.Code)
            .ToList();

        var glCodeToId = new Dictionary<string, int>();

        foreach (var (code, title, groupCode) in distinctGl)
        {
            var existing = await _db.GeneralAccounts
                .FirstOrDefaultAsync(g => g.Code == code, ct);

            if (existing != null)
            {
                glCodeToId[code] = existing.Id;
                continue;
            }

            var groupId = groupCodeToId.GetValueOrDefault(groupCode);
            if (groupId == 0)
            {
                _logger.LogWarning("GL {Code}: parent group {GroupCode} not found, skipping.", code, groupCode);
                continue;
            }

            var entity = new GeneralAccount
            {
                AccountGroupId = groupId,
                Code = code,
                Name = title,
                IsActive = true,
            };
            _db.GeneralAccounts.Add(entity);
            await _db.SaveChangesAsync(ct);
            glCodeToId[code] = entity.Id;
        }
        _logger.LogInformation("Seeded {Count} GeneralAccounts.", distinctGl.Count);

        // ── 4. Reload GL mapping after all GLs are committed ──
        var glDict = await _db.GeneralAccounts
            .AsNoTracking()
            .ToDictionaryAsync(g => g.Code.Trim(), g => g.Id, ct);

        // ── 5. Build SLs ──
        // SL code is the full 6-char code (e.g. "111001" from full "111110111001")
        var distinctSl = rows
            .Select(r => (
                Code: r.SrcSlCode,
                Title: r.SlTitle,
                GlCode: r.SrcGlCode,
                GroupCode: r.SrcGroupCode))
            .Distinct()
            .OrderBy(s => s.Code)
            .ToList();

        var existingSlCodes = await _db.SubsidiaryAccounts
            .Select(s => s.Code)
            .ToHashSetAsync(ct);

        var newSlList = new List<SubsidiaryAccount>(distinctSl.Count);

        foreach (var (code, title, glCode, groupCode) in distinctSl)
        {
            if (existingSlCodes.Contains(code))
                continue;

            if (!glDict.TryGetValue(glCode.Trim(), out var parentGlId))
            {
                _logger.LogWarning("SL {Code}: parent GL {GlCode} not found, skipping.", code, glCode);
                continue;
            }

            var systemKey = $"SL_{code.Trim()}";

            // Nature: groups starting with 2, 3, 4 → Credit; otherwise Debit
            var nature = groupCode.StartsWith("2") || groupCode.StartsWith("3") || groupCode.StartsWith("4")
                ? AccountNature.Credit
                : AccountNature.Debit;

            // Temporary (P&L): groups 4 (Sales), 5 (COGS), 6 (Expenses), 8 (Other)
            bool isTemp = groupCode.StartsWith("4") || groupCode.StartsWith("5") ||
                          groupCode.StartsWith("6") || groupCode.StartsWith("8");

            newSlList.Add(new SubsidiaryAccount
            {
                GeneralAccountId = parentGlId,
                Code = code.Trim(),
                Name = title.Trim(),
                SystemKey = systemKey,
                Nature = nature,
                IsActive = true,
                AllowsFloatingDetail = true,
                IsTemporary = isTemp
            });
        }

        if (newSlList.Count == 0)
        {
            _logger.LogInformation("No new SubsidiaryAccounts to seed.");
        }
        else
        {
            _db.SubsidiaryAccounts.AddRange(newSlList);

            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Successfully inserted {Count} SubsidiaryAccounts (SL).", newSlList.Count);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save SL records: {Message}. Inner: {Inner}",
                    ex.Message, ex.InnerException?.Message);
                throw;
            }
        }

        _logger.LogInformation(
            "ChartOfAccounts seeding complete: {GroupCount} Groups, {GlCount} GLs, {SlCount} SLs.",
            distinctGroups.Count, distinctGl.Count, distinctSl.Count);
    }

    /// <summary>
    /// Removes any dummy/placeholder seed records whose codes don't match
    /// the actual codes in hesab.txt. This prevents FK conflicts.
    /// </summary>
    private async Task ClearDummySeedDataAsync(List<RawRow> rows, CancellationToken ct)
    {
        var realGroupCodes = rows.Select(r => r.SrcGroupCode).Distinct().ToHashSet();
        var realGlCodes = rows.Select(r => r.SrcGlCode).Distinct().ToHashSet();
        var realSlCodes = rows.Select(r => r.SrcSlCode).Distinct().ToHashSet();

        // Delete SLs whose codes are not in hesab.txt
        var dummySls = await _db.SubsidiaryAccounts
            .Where(s => !realSlCodes.Contains(s.Code))
            .ToListAsync(ct);
        if (dummySls.Count > 0)
        {
            _db.SubsidiaryAccounts.RemoveRange(dummySls);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Removed {Count} dummy SubsidiaryAccounts.", dummySls.Count);
        }

        // Delete GLs whose codes are not in hesab.txt
        var dummyGls = await _db.GeneralAccounts
            .Where(g => !realGlCodes.Contains(g.Code))
            .ToListAsync(ct);
        if (dummyGls.Count > 0)
        {
            _db.GeneralAccounts.RemoveRange(dummyGls);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Removed {Count} dummy GeneralAccounts.", dummyGls.Count);
        }

        // Delete Groups whose codes are not in hesab.txt
        var dummyGroups = await _db.AccountGroups
            .Where(g => !realGroupCodes.Contains(g.Code))
            .ToListAsync(ct);
        if (dummyGroups.Count > 0)
        {
            _db.AccountGroups.RemoveRange(dummyGroups);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Removed {Count} dummy AccountGroups.", dummyGroups.Count);
        }
    }
}