using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services;

public interface IDocumentVersioningService
{
    Task<DocumentVersion> CreateVersionAsync(
        DocumentArchiveItem document,
        IFormFile? newFile,
        string? changeSummary,
        string userId,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetVersionFileAsync(int versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentVersion>> GetVersionHistoryAsync(int documentId, CancellationToken cancellationToken = default);

    Task<DocumentArchiveItem> RollbackToVersionAsync(int documentId, int versionId, string userId, CancellationToken cancellationToken = default);
}

public sealed class DocumentVersioningService : IDocumentVersioningService
{
    private readonly IWorkflowDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly Tenancy.ITenantPathResolver _tenantPathResolver;

    public DocumentVersioningService(
        IWorkflowDbContext context,
        IWebHostEnvironment environment,
        Tenancy.ITenantPathResolver tenantPathResolver)
    {
        _context = context;
        _environment = environment;
        _tenantPathResolver = tenantPathResolver;
    }

    public async Task<DocumentVersion> CreateVersionAsync(
        DocumentArchiveItem document,
        IFormFile? newFile,
        string? changeSummary,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Increment version number
        var currentVersion = ParseVersion(document.CurrentVersion);
        var newVersion = IncrementVersion(currentVersion);

        // Save the old file as a version record
        var version = new DocumentVersion
        {
            DocumentArchiveItemId = document.Id,
            VersionNumber = document.CurrentVersion,
            FilePath = document.StoredFileName,
            StoredFileName = document.StoredFileName,
            RelativePath = document.RelativePath,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            ChangeSummary = changeSummary?.Trim(),
            CreatedByUserId = userId,
            CreatedAt = DateTime.Now
        };

        _context.DocumentVersions.Add(version);

        // Update document with new version info
        document.CurrentVersion = newVersion;

        if (newFile != null)
        {
            var extension = Path.GetExtension(newFile.FileName);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var uploadRoot = _tenantPathResolver.GetArchiveRoot(Path.Combine(_environment.ContentRootPath, "App_Data"));
            Directory.CreateDirectory(uploadRoot);
            var fullPath = Path.Combine(uploadRoot, storedFileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await newFile.CopyToAsync(stream, cancellationToken);
            }

            document.FileName = newFile.FileName;
            document.StoredFileName = storedFileName;
            document.RelativePath = _tenantPathResolver.GetTenantRelativePath("uploads", "archive", storedFileName);
            document.ContentType = newFile.ContentType ?? "application/octet-stream";
            document.FileSize = newFile.Length;
            document.IsPreviewable = IsPreviewableContentType(document.ContentType);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<byte[]?> GetVersionFileAsync(int versionId, CancellationToken cancellationToken = default)
    {
        var version = await _context.DocumentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

        if (version == null) return null;

        var physicalPath = _tenantPathResolver.MapRelativeToPhysical(
            Path.Combine(_environment.ContentRootPath, "App_Data"), version.RelativePath);

        if (!File.Exists(physicalPath))
        {
            physicalPath = _tenantPathResolver.MapRelativeToPhysical(
                _environment.WebRootPath, version.RelativePath);
            if (!File.Exists(physicalPath))
                return null;
        }

        return await File.ReadAllBytesAsync(physicalPath, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentVersion>> GetVersionHistoryAsync(
        int documentId, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentVersions
            .AsNoTracking()
            .Include(v => v.CreatedByUser)
            .Where(v => v.DocumentArchiveItemId == documentId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentArchiveItem> RollbackToVersionAsync(
        int documentId, int versionId, string userId, CancellationToken cancellationToken = default)
    {
        var document = await _context.DocumentArchiveItems
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new InvalidOperationException("سند یافت نشد.");

        var targetVersion = await _context.DocumentVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.DocumentArchiveItemId == documentId, cancellationToken)
            ?? throw new InvalidOperationException("نسخه مورد نظر یافت نشد.");

        // Save current state as a new version before rollback
        var currentVersion = new DocumentVersion
        {
            DocumentArchiveItemId = document.Id,
            VersionNumber = document.CurrentVersion,
            FilePath = document.StoredFileName,
            StoredFileName = document.StoredFileName,
            RelativePath = document.RelativePath,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            ChangeSummary = $"بازگشت به نسخه {targetVersion.VersionNumber}",
            CreatedByUserId = userId,
            CreatedAt = DateTime.Now
        };
        _context.DocumentVersions.Add(currentVersion);

        // Rollback document fields
        var newVersion = IncrementVersion(ParseVersion(document.CurrentVersion));
        document.CurrentVersion = newVersion;
        document.FileName = Path.GetFileName(targetVersion.FilePath);
        document.StoredFileName = targetVersion.StoredFileName;
        document.RelativePath = targetVersion.RelativePath;
        document.ContentType = targetVersion.ContentType;
        document.FileSize = targetVersion.FileSize;
        document.IsPreviewable = IsPreviewableContentType(targetVersion.ContentType ?? "application/octet-stream");

        await _context.SaveChangesAsync(cancellationToken);
        return document;
    }

    private static decimal ParseVersion(string version)
    {
        return decimal.TryParse(version, out var v) ? v : 1.0m;
    }

    private static string IncrementVersion(decimal current)
    {
        return (current + 0.1m).ToString("0.0");
    }

    private static bool IsPreviewableContentType(string contentType)
    {
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }
}