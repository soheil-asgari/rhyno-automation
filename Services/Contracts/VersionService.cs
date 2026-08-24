using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public sealed class VersionService : IVersionService
{
    private readonly OfficeDbContext _context;
    private readonly ILogger<VersionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> SignificantFields = new(StringComparer.Ordinal)
    {
        "totalAmount", "endDate", "startDate", "status", "paymentStatus",
        "contractNumber", "contractType", "signDate"
    };

    public VersionService(OfficeDbContext context, ILogger<VersionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private async Task<ContractSnapshot> ExtractSnapshotAsync(Contract contract, CancellationToken ct)
    {
        var snapshot = new ContractSnapshot
        {
            ContractNumber = contract.ContractNumber,
            Title = contract.Title,
            ContractType = contract.ContractType,
            CustomerId = contract.CustomerId,
            ClientName = contract.ClientName,
            ClientPhone = contract.ClientPhone,
            ClientAddress = contract.ClientAddress,
            Description = contract.Description,
            Notes = contract.Notes,
            TermsAndConditions = contract.TermsAndConditions,
            ContractBodyHtml = contract.ContractBodyHtml,
            TotalAmount = contract.TotalAmount,
            PaidAmount = contract.PaidAmount,
            RemainingBalance = contract.RemainingBalance,
            PaymentStatus = contract.PaymentStatus,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            SignDate = contract.SignDate,
            ExpiryNoticeDays = contract.ExpiryNoticeDays,
            Status = contract.Status,
            AssignedUserId = contract.AssignedUserId,
            TemplateId = contract.TemplateId,
            IsArchived = contract.IsArchived
        };

        // ── Capture Payment Schedule data ───────────────────────
        var schedule = await _context.Set<ContractPaymentSchedule>()
            .AsNoTracking()
            .Include(s => s.Installments)
            .FirstOrDefaultAsync(s => s.ContractId == contract.Id, ct);

        if (schedule != null)
        {
            snapshot.PaymentSchedule = new PaymentScheduleSnapshot
            {
                ScheduleType = schedule.ScheduleType,
                InstallmentCount = schedule.InstallmentCount,
                TotalAmount = schedule.TotalAmount,
                Installments = schedule.Installments.OrderBy(i => i.InstallmentNumber)
                    .Select(i => new InstallmentSnapshotLine
                    {
                        InstallmentNumber = i.InstallmentNumber,
                        DueDate = i.DueDate,
                        Amount = i.Amount,
                        Percentage = i.Percentage,
                        Description = i.Description,
                        Status = i.Status,
                        PaidAmount = i.PaidAmount,
                    }).ToList()
            };
        }

        return snapshot;
    }

    public async Task<ContractVersion?> CreateVersionSnapshotAsync(
        Contract contract, string currentUserId,
        string changeType = "Edited",
        string? changeDescription = null,
        bool isMajor = false,
        bool skipIfUnchanged = false,
        CancellationToken ct = default)
    {
        var latestVersion = await _context.Set<ContractVersion>()
            .AsNoTracking()
            .Where(v => v.ContractId == contract.Id)
            .OrderByDescending(v => v.MajorVersion)
            .ThenByDescending(v => v.MinorVersion)
            .FirstOrDefaultAsync(ct);

        // Extract the snapshot from the current contract state
        var snapshot = await ExtractSnapshotAsync(contract, ct);
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);

        if (skipIfUnchanged && latestVersion != null)
        {
            if (string.Equals(latestVersion.SnapshotData, snapshotJson, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "Skipping version snapshot for Contract {ContractId} — no changes since v{VersionLabel}",
                    contract.Id, latestVersion.VersionLabel);
                return null;
            }
        }

        int major = 1, minor = 0;
        if (latestVersion != null)
        {
            if (isMajor) { major = latestVersion.MajorVersion + 1; minor = 0; }
            else { major = latestVersion.MajorVersion; minor = latestVersion.MinorVersion + 1; }
        }

        var version = new ContractVersion
        {
            ContractId = contract.Id,
            MajorVersion = major,
            MinorVersion = minor,
            SnapshotData = snapshotJson,
            ChangeType = changeType,
            ChangeDescription = changeDescription,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = currentUserId,
        };

        _context.Set<ContractVersion>().Add(version);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Version snapshot {V} created for Contract {Id}: {Desc}",
            version.VersionLabel, contract.Id, changeDescription ?? changeType);
        return version;
    }

    public async Task<ContractVersion?> GetLatestVersionAsync(int contractId, CancellationToken ct = default)
    {
        return await _context.Set<ContractVersion>()
            .AsNoTracking()
            .Include(v => v.CreatedByUser)
            .Where(v => v.ContractId == contractId)
            .OrderByDescending(v => v.MajorVersion)
            .ThenByDescending(v => v.MinorVersion)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<ContractVersion>> GetVersionsForContractAsync(int contractId, CancellationToken ct = default)
    {
        return await _context.Set<ContractVersion>()
            .AsNoTracking()
            .Include(v => v.CreatedByUser)
            .Where(v => v.ContractId == contractId)
            .OrderByDescending(v => v.MajorVersion)
            .ThenByDescending(v => v.MinorVersion)
            .ToListAsync(ct);
    }

    public async Task<ContractVersion?> GetVersionByIdAsync(int versionId, CancellationToken ct = default)
    {
        return await _context.Set<ContractVersion>()
            .AsNoTracking()
            .Include(v => v.CreatedByUser)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
    }

    public async Task<List<VersionDiffEntry>> CompareVersionsAsync(
        int versionIdA, int versionIdB, CancellationToken ct = default)
    {
        var vA = await _context.Set<ContractVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionIdA, ct);
        var vB = await _context.Set<ContractVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionIdB, ct);

        if (vA == null || vB == null)
            throw new KeyNotFoundException("One or both versions not found.");

        var oldSnapshot = JsonSerializer.Deserialize<ContractSnapshot>(vA.SnapshotData, JsonOptions)
            ?? new ContractSnapshot();
        var newSnapshot = JsonSerializer.Deserialize<ContractSnapshot>(vB.SnapshotData, JsonOptions)
            ?? new ContractSnapshot();

        return DiffSnapshots(oldSnapshot, newSnapshot);
    }

    public async Task<ContractVersion?> GetVersionAsOfAsync(
        int contractId, DateTime asOfDate, CancellationToken ct = default)
    {
        var asOfUtc = asOfDate.ToUniversalTime();

        return await _context.Set<ContractVersion>()
            .AsNoTracking()
            .Include(v => v.CreatedByUser)
            .Where(v => v.ContractId == contractId && v.CreatedAt <= asOfUtc)
            .OrderByDescending(v => v.MajorVersion)
            .ThenByDescending(v => v.MinorVersion)
            .FirstOrDefaultAsync(ct);
    }

    private static List<VersionDiffEntry> DiffSnapshots(ContractSnapshot oldSnapshot, ContractSnapshot newSnapshot)
    {
        var diffs = new List<VersionDiffEntry>();

        CompareField(diffs, "شماره قرارداد", oldSnapshot.ContractNumber, newSnapshot.ContractNumber);
        CompareField(diffs, "عنوان", oldSnapshot.Title, newSnapshot.Title);
        CompareField(diffs, "نوع قرارداد", oldSnapshot.ContractType, newSnapshot.ContractType);
        CompareField(diffs, "شناسه مشتری", oldSnapshot.CustomerId?.ToString(), newSnapshot.CustomerId?.ToString());
        CompareField(diffs, "طرف قرارداد", oldSnapshot.ClientName, newSnapshot.ClientName);
        CompareField(diffs, "تلفن طرف", oldSnapshot.ClientPhone, newSnapshot.ClientPhone);
        CompareField(diffs, "آدرس طرف", oldSnapshot.ClientAddress, newSnapshot.ClientAddress);
        CompareField(diffs, "توضیحات", oldSnapshot.Description, newSnapshot.Description);
        CompareField(diffs, "یادداشت‌ها", oldSnapshot.Notes, newSnapshot.Notes);
        CompareField(diffs, "شرایط و ضوابط", oldSnapshot.TermsAndConditions, newSnapshot.TermsAndConditions);
        CompareField(diffs, "متن قرارداد", oldSnapshot.ContractBodyHtml, newSnapshot.ContractBodyHtml);
        CompareField(diffs, "مبلغ کل", oldSnapshot.TotalAmount.ToString("N2"), newSnapshot.TotalAmount.ToString("N2"));
        CompareField(diffs, "مبلغ پرداخت شده", oldSnapshot.PaidAmount.ToString("N2"), newSnapshot.PaidAmount.ToString("N2"));
        CompareField(diffs, "باقی مانده", oldSnapshot.RemainingBalance.ToString("N2"), newSnapshot.RemainingBalance.ToString("N2"));
        CompareField(diffs, "وضعیت پرداخت", oldSnapshot.PaymentStatus, newSnapshot.PaymentStatus);
        CompareField(diffs, "تاریخ شروع", oldSnapshot.StartDate.ToString("yyyy-MM-dd"), newSnapshot.StartDate.ToString("yyyy-MM-dd"));
        CompareField(diffs, "تاریخ پایان", oldSnapshot.EndDate?.ToString("yyyy-MM-dd"), newSnapshot.EndDate?.ToString("yyyy-MM-dd"));
        CompareField(diffs, "تاریخ امضا", oldSnapshot.SignDate?.ToString("yyyy-MM-dd"), newSnapshot.SignDate?.ToString("yyyy-MM-dd"));
        CompareField(diffs, "روزهای اخطار انقضا", oldSnapshot.ExpiryNoticeDays.ToString(), newSnapshot.ExpiryNoticeDays.ToString());
        CompareField(diffs, "وضعیت", oldSnapshot.Status, newSnapshot.Status);
        CompareField(diffs, "مسئول پیگیری", oldSnapshot.AssignedUserId, newSnapshot.AssignedUserId);
        CompareField(diffs, "قالب", oldSnapshot.TemplateId?.ToString(), newSnapshot.TemplateId?.ToString());
        CompareField(diffs, "بایگانی", oldSnapshot.IsArchived.ToString(), newSnapshot.IsArchived.ToString());

        // ── Payment Schedule diff ───────────────────────────────
        var oldPs = oldSnapshot.PaymentSchedule;
        var newPs = newSnapshot.PaymentSchedule;
        if (oldPs != null || newPs != null)
        {
            var oldType = oldPs?.ScheduleType ?? "(ندارد)";
            var newType = newPs?.ScheduleType ?? "(ندارد)";
            CompareField(diffs, "نوع برنامه پرداخت", oldType, newType);

            var oldCount = oldPs?.InstallmentCount.ToString() ?? "0";
            var newCount = newPs?.InstallmentCount.ToString() ?? "0";
            CompareField(diffs, "تعداد اقساط", oldCount, newCount);

            var oldTotal = oldPs?.TotalAmount.ToString("N2") ?? "0";
            var newTotal = newPs?.TotalAmount.ToString("N2") ?? "0";
            CompareField(diffs, "مبلغ کل برنامه", oldTotal, newTotal);

            // Compare installment status summary
            var oldPaid = oldPs?.Installments.Count(i => i.Status == "Paid").ToString() ?? "0";
            var newPaid = newPs?.Installments.Count(i => i.Status == "Paid").ToString() ?? "0";
            CompareField(diffs, "تعداد اقساط پرداخت‌شده", oldPaid, newPaid);

            var oldOverdue = oldPs?.Installments.Count(i => i.Status == "Overdue").ToString() ?? "0";
            var newOverdue = newPs?.Installments.Count(i => i.Status == "Overdue").ToString() ?? "0";
            CompareField(diffs, "تعداد اقساط سررسید گذشته", oldOverdue, newOverdue);
        }

        return diffs;
    }

    private static void CompareField(
        List<VersionDiffEntry> diffs, string fieldName,
        string? oldVal, string? newVal)
    {
        var normalizedOld = string.IsNullOrWhiteSpace(oldVal) ? null : oldVal;
        var normalizedNew = string.IsNullOrWhiteSpace(newVal) ? null : newVal;

        if (string.Equals(normalizedOld, normalizedNew, StringComparison.Ordinal))
            return;

        var isSignificant = SignificantFields.Contains(
            fieldName switch
            {
                "مبلغ کل" => "totalAmount",
                "تاریخ پایان" => "endDate",
                "تاریخ شروع" => "startDate",
                "وضعیت" => "status",
                "وضعیت پرداخت" => "paymentStatus",
                "شماره قرارداد" => "contractNumber",
                "نوع قرارداد" => "contractType",
                "تاریخ امضا" => "signDate",
                _ => ""
            });

        diffs.Add(new VersionDiffEntry
        {
            FieldName = fieldName,
            OldValue = normalizedOld,
            NewValue = normalizedNew,
            IsSignificant = isSignificant
        });
    }
}
