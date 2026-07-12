using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Tenancy;

public interface ITenantPathResolver
{
    string GetWorkflowUploadRoot(string webRootPath, string documentType, int documentId);
    string GetArchiveRoot(string webRootPath);
    string GetTenantRelativePath(params string[] segments);
    string MapRelativeToPhysical(string webRootPath, string relativePath);
}

public sealed class TenantPathResolver : ITenantPathResolver
{
    private readonly ITenantIsolationService _tenantIsolationService;

    public TenantPathResolver(ITenantIsolationService tenantIsolationService)
    {
        _tenantIsolationService = tenantIsolationService;
    }

    public string GetWorkflowUploadRoot(string webRootPath, string documentType, int documentId)
    {
        return Path.Combine(webRootPath, GetTenantStorageRoot(), "uploads", "workflow", documentType.ToLowerInvariant(), documentId.ToString());
    }

    public string GetArchiveRoot(string webRootPath)
    {
        return Path.Combine(webRootPath, GetTenantStorageRoot(), "uploads", "archive");
    }

    public string GetTenantRelativePath(params string[] segments)
    {
        var relative = Path.Combine([GetTenantStorageRoot(), .. segments]).Replace('\\', '/');
        return "/" + relative.TrimStart('/');
    }

    public string MapRelativeToPhysical(string webRootPath, string relativePath)
    {
        var resolvedWebRoot = Path.GetFullPath(webRootPath);
        var combinedPath = Path.Combine(resolvedWebRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.GetFullPath(combinedPath);

        if (!fullPath.StartsWith(resolvedWebRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path traversal detected.");
        }

        return fullPath;
    }

    private string GetTenantStorageRoot()
    {
        return _tenantIsolationService.GetCurrent().StorageRoot;
    }
}
