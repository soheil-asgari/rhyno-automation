using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public sealed class DocumentRequirementService : IDocumentRequirementService
{
    private readonly OfficeDbContext _context;
    private readonly ILogger<DocumentRequirementService> _logger;

    public DocumentRequirementService(
        OfficeDbContext context,
        ILogger<DocumentRequirementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<DocumentRequirement>> GetRequirementsForTypeAsync(string contractType, CancellationToken ct = default)
    {
        return await _context.DocumentRequirements
            .AsNoTracking()
            .Where(d => d.ContractType == contractType)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.DocumentName)
            .ToListAsync(ct);
    }

    public async Task<DocumentRequirementChecklistVM> GetChecklistAsync(int contractId, CancellationToken ct = default)
    {
        var contract = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new KeyNotFoundException($"Contract {contractId} not found.");

        var requirements = await GetRequirementsForTypeAsync(contract.ContractType ?? "", ct);

        var items = requirements.Select(r =>
        {
            var matchingAttachment = contract.Attachments?
                .FirstOrDefault(a => a.DocumentRequirementId == r.Id);

            return new DocumentRequirementItemVM
            {
                Id = r.Id,
                DocumentName = r.DocumentName,
                DocumentCategory = r.DocumentCategory,
                IsRequired = r.IsRequired,
                Description = r.Description,
                IsUploaded = matchingAttachment != null,
                UploadedAttachmentId = matchingAttachment?.Id,
                UploadedFileName = matchingAttachment?.FileName
            };
        }).ToList();

        return new DocumentRequirementChecklistVM
        {
            ContractId = contractId,
            ContractType = contract.ContractType,
            Items = items
        };
    }

    public async Task<(bool AllUploaded, List<string> MissingNames)> ValidateRequiredDocumentsAsync(int contractId, CancellationToken ct = default)
    {
        var checklist = await GetChecklistAsync(contractId, ct);
        var missing = checklist.Items
            .Where(i => i.IsRequired && !i.IsUploaded)
            .Select(i => i.DocumentName)
            .ToList();
        return (missing.Count == 0, missing);
    }

    public async Task<int> SeedDefaultRequirementsAsync(string contractType, CancellationToken ct = default)
    {
        var existing = await _context.DocumentRequirements
            .Where(d => d.ContractType == contractType)
            .AnyAsync(ct);

        if (existing) return 0;

        var seeds = GetDefaultSeeds(contractType);
        if (seeds.Count == 0) return 0;

        _context.DocumentRequirements.AddRange(seeds);
        var count = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} document requirements for contract type '{Type}'.", count, contractType);
        return count;
    }

    public async Task<int> SeedAllDefaultsAsync(CancellationToken ct = default)
    {
        int total = 0;
        foreach (var type in ContractTypes.All)
        {
            total += await SeedDefaultRequirementsAsync(type, ct);
        }
        return total;
    }

    public async Task<DocumentRequirement> SaveRequirementAsync(DocumentRequirement requirement, CancellationToken ct = default)
    {
        if (requirement.Id == 0)
        {
            _context.DocumentRequirements.Add(requirement);
        }
        else
        {
            _context.DocumentRequirements.Update(requirement);
        }
        await _context.SaveChangesAsync(ct);
        return requirement;
    }

    public async Task<bool> DeleteRequirementAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.DocumentRequirements.FindAsync(new object[] { id }, ct);
        if (entity == null) return false;
        _context.DocumentRequirements.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static List<DocumentRequirement> GetDefaultSeeds(string contractType)
    {
        var list = new List<DocumentRequirement>();
        int order = 1;

        switch (contractType)
        {
            case var t when t == ContractTypes.Lease:
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی موجر", IsRequired = true, Description = "کپی کارت ملی مالک/موجر", DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "سند مالکیت", IsRequired = true, Description = "سند رسمی ملک", DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Legal, DocumentName = "جواز کسب / پروانه بهره‌برداری", IsRequired = false, Description = "در صورت وجود", DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Insurance, DocumentName = "بیمه نامه مسئولیت", IsRequired = false, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "شناسنامه و کارت ملی مستأجر", IsRequired = true, DisplayOrder = order++ });
                break;
            case var t when t == ContractTypes.NDA:
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "صفحه امضای NDA", IsRequired = true, Description = "امضای طرفین", DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی طرفین", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Legal, DocumentName = "آگهی تاسیس / روزنامه رسمی (شرکت‌ها)", IsRequired = false, Description = "برای اشخاص حقوقی", DisplayOrder = order++ });
                break;
            case var t when t == ContractTypes.Sales:
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "قرارداد فروش امضا شده", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی خریدار و فروشنده", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Financial, DocumentName = "فاکتور رسمی / پیش‌فاکتور", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Insurance, DocumentName = "بیمه نامه کالا (در صورت وجود)", IsRequired = false, DisplayOrder = order++ });
                break;
            case var t when t == ContractTypes.Purchase:
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "قرارداد خرید امضا شده", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی طرفین", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Financial, DocumentName = "پیش‌فاکتور / استعلام قیمت", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Technical, DocumentName = "مشخصات فنی کالا/خدمات", IsRequired = false, DisplayOrder = order++ });
                break;
            case var t when t == ContractTypes.Service:
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "قرارداد خدمات امضا شده", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی طرفین", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Technical, DocumentName = "شرح خدمات / SLA", IsRequired = true, Description = "سند سطح خدمات", DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Insurance, DocumentName = "بیمه نامه مسئولیت حرفه‌ای", IsRequired = false, DisplayOrder = order++ });
                break;
            case var t when t == ContractTypes.Partnership:
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "قرارداد مشارکت امضا شده", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی / شناسنامه طرفین", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Financial, DocumentName = "صورت‌های مالی (دو سال اخیر)", IsRequired = false, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Legal, DocumentName = "آگهی تاسیس / اساسنامه", IsRequired = false, DisplayOrder = order++ });
                break;
            case var t when t == ContractTypes.Maintenance:
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "قرارداد نگهداری امضا شده", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی طرفین", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Technical, DocumentName = "چک‌لیست تجهیزات تحت پوشش", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Insurance, DocumentName = "بیمه نامه تجهیزات", IsRequired = false, DisplayOrder = order++ });
                break;
            case var t when t == ContractTypes.Consulting:
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "قرارداد مشاوره امضا شده", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی طرفین", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = t, DocumentCategory = DocumentRequirementCategories.Technical, DocumentName = "گزارش پیشنهاد فنی", IsRequired = false, DisplayOrder = order++ });
                break;
            default:
                list.Add(new() { ContractType = contractType, DocumentCategory = DocumentRequirementCategories.Agreement, DocumentName = "قرارداد امضا شده", IsRequired = true, DisplayOrder = order++ });
                list.Add(new() { ContractType = contractType, DocumentCategory = DocumentRequirementCategories.Identification, DocumentName = "کارت ملی طرفین", IsRequired = true, DisplayOrder = order++ });
                break;
        }

        return list;
    }

}