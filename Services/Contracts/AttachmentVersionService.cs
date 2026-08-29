using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public sealed class AttachmentVersionService : IAttachmentVersionService
{
    private readonly OfficeDbContext _context;
    private readonly IContractService _contractService;
    private readonly ILogger<AttachmentVersionService> _logger;

    private static readonly string UploadsRoot = Path.Combine("wwwroot", "uploads", "contracts");

    public AttachmentVersionService(
        OfficeDbContext context,
        IContractService contractService,
        ILogger<AttachmentVersionService> logger)
    {
        _context = context;
        _contractService = contractService;
        _logger = logger;
    }

    public async Task<ContractAttachmentVersion> UploadNewVersionAsync(
        int attachmentId, string fileName, string contentType, long fileSize,
        Stream fileStream, string currentUserId, string? changeDescription = null,
        CancellationToken ct = default)
    {
        var attachment = await _context.ContractAttachments
            .Include(a => a.Contract)
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw new InvalidOperationException("Ù¾ÛŒÙˆØ³Øª ÛŒØ§ÙØª Ù†Ø´Ø¯.");

        await _contractService.EnforceAccessAsync(attachment!.ContractId, currentUserId, ct);

        var maxVersion = await _context.ContractAttachmentVersions
            .Where(v => v.AttachmentId == attachmentId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        var nextVersion = maxVersion + 1;

        var ext = Path.GetExtension(fileName);
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var contractDir = Path.Combine(UploadsRoot, attachment.ContractId.ToString());
        var versionDir = Path.Combine(contractDir, "versions");
        Directory.CreateDirectory(versionDir);
        var fullPath = Path.Combine(versionDir, storedFileName);

        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        var relativePath = Path.Combine("uploads", "contracts", attachment.ContractId.ToString(), "versions", storedFileName);

        var version = new ContractAttachmentVersion
        {
            AttachmentId = attachmentId,
            VersionNumber = nextVersion,
            FileName = fileName,
            StoredFileName = storedFileName,
            RelativePath = relativePath,
            ContentType = contentType,
            FileSize = fileSize,
            ChangeDescription = changeDescription,
            UploadedByUserId = currentUserId
        };

        _context.ContractAttachmentVersions.Add(version);
        await _context.SaveChangesAsync(ct);

        attachment.CurrentVersionId = version.Id;
        attachment.VersionNumber = nextVersion;
        attachment.FileName = fileName;
        attachment.StoredFileName = storedFileName;
        attachment.RelativePath = relativePath;
        attachment.ContentType = contentType;
        attachment.FileSize = fileSize;

        await _context.SaveChangesAsync(ct);

        return version;
    }

    public async Task<List<AttachmentVersionVM>> GetVersionsAsync(int attachmentId, CancellationToken ct = default)
    {
        var attachment = await _context.ContractAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

        if (attachment == null) return new();

        var versions = await _context.ContractAttachmentVersions
            .AsNoTracking()
            .Include(v => v.UploadedByUser)
            .Where(v => v.AttachmentId == attachmentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new AttachmentVersionVM
            {
                Id = v.Id,
                AttachmentId = v.AttachmentId,
                VersionNumber = v.VersionNumber,
                FileName = v.FileName,
                ContentType = v.ContentType,
                FileSize = v.FileSize,
                FileSizeText = FormatFileSize(v.FileSize),
                ChangeDescription = v.ChangeDescription,
                UploadedByUserName = v.UploadedByUser != null
                    ? v.UploadedByUser.FullName ?? v.UploadedByUser.UserName ?? "\u0646\u0627\u0645\u0634\u062E\u0635"
                    : "\u0646\u0627\u0645\u0634\u062E\u0635",
                CreatedAtText = ToPersianDate(v.CreatedAt),
                IsCurrent = v.Id == attachment.CurrentVersionId
            })
            .ToListAsync(ct);

        return versions;
    }

    public async Task<string?> GetVersionFilePathAsync(int versionId, string userId, CancellationToken ct = default)
    {
        var version = await _context.ContractAttachmentVersions
            .AsNoTracking()
            .Include(v => v.Attachment)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);

        if (version?.Attachment == null) return null;

        await _contractService.EnforceAccessAsync(version.Attachment.ContractId, userId, ct);

        // Resolve path relative to the wwwroot directory
        // version.RelativePath is stored as "uploads/contracts/{id}/versions/{file}" (no wwwroot prefix)
        var wwwRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
        var fullPath = Path.GetFullPath(Path.Combine(wwwRoot, version.RelativePath));
        var uploadsRoot = Path.GetFullPath(Path.Combine(wwwRoot, "uploads", "contracts"));

        if (!fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(fullPath) ? fullPath : null;
    }

    public async Task<ContractAttachmentVersion> RestoreVersionAsync(
        int versionId, string currentUserId, CancellationToken ct = default)
    {
        var oldVersion = await _context.ContractAttachmentVersions
            .Include(v => v.Attachment)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new InvalidOperationException("\u0646\u0633\u062E\u0647 \u0645\u0648\u0631\u062F \u0646\u0638\u0631 \u06CC\u0627\u0641\u062A \u0646\u0634\u062F.");

        var attachment = oldVersion.Attachment!;
        await _contractService.EnforceAccessAsync(attachment.ContractId, currentUserId, ct);

        var oldFilePath = await GetVersionFilePathAsync(versionId, currentUserId, ct);
        if (oldFilePath == null)
            throw new InvalidOperationException("\u0641\u0627\u06CC\u0644 \u0646\u0633\u062E\u0647 \u0642\u062F\u06CC\u0645\u06CC \u06CC\u0627\u0641\u062A \u0646\u0634\u062F.");

        var fileStream = new FileStream(oldFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using (fileStream)
        {
            return await UploadNewVersionAsync(
                attachment.Id, oldVersion.FileName,
                oldVersion.ContentType ?? "application/octet-stream",
                oldVersion.FileSize, fileStream, currentUserId,
                $"\u0628\u0627\u0632\u06AF\u0631\u062F\u0627\u0646\u06CC \u0628\u0647 \u0646\u0633\u062E\u0647 {oldVersion.VersionNumber}",
                ct);
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string ToPersianDate(DateTimeOffset value)
    {
        var local = value.ToLocalTime().DateTime;
        var calendar = new PersianCalendar();
        return $"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00}";
    }
}