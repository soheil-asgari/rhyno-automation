using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Tenancy;

public sealed record TenantQuotaResult(bool IsAllowed, string? Reason)
{
    public static TenantQuotaResult Allowed { get; } = new(true, null);
}

public interface ITenantQuotaService
{
    Task<TenantQuotaResult> ValidateWriteAsync(TenantDefinition tenant, CancellationToken cancellationToken = default);
}

public sealed class TenantQuotaService : ITenantQuotaService
{
    private readonly IdentityDbContext _identityContext;
    private readonly ITenantPathResolver _tenantPathResolver;
    private readonly IWebHostEnvironment _environment;

    public TenantQuotaService(
        IdentityDbContext identityContext,
        ITenantPathResolver tenantPathResolver,
        IWebHostEnvironment environment)
    {
        _identityContext = identityContext;
        _tenantPathResolver = tenantPathResolver;
        _environment = environment;
    }

    public async Task<TenantQuotaResult> ValidateWriteAsync(TenantDefinition tenant, CancellationToken cancellationToken = default)
    {
        if (tenant.MaxActiveUsers is int maxUsers)
        {
            var activeUsers = await _identityContext.Users
                .AsNoTracking()
                .CountAsync(item => !item.LockoutEnd.HasValue || item.LockoutEnd <= DateTimeOffset.UtcNow, cancellationToken);

            if (activeUsers > maxUsers)
            {
                return new TenantQuotaResult(false, $"Tenant active user quota exceeded ({activeUsers}/{maxUsers}).");
            }
        }

        if (tenant.MaxStorageMegabytes is long maxStorageMb)
        {
            var relativeRoot = _tenantPathResolver.GetTenantRelativePath();
            var storageRoot = _tenantPathResolver.MapRelativeToPhysical(_environment.WebRootPath, relativeRoot);
            if (Directory.Exists(storageRoot))
            {
                var bytes = Directory.EnumerateFiles(storageRoot, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
                var usedMb = bytes / 1024 / 1024;
                if (usedMb > maxStorageMb)
                {
                    return new TenantQuotaResult(false, $"Tenant storage quota exceeded ({usedMb}/{maxStorageMb} MB).");
                }
            }
        }

        return TenantQuotaResult.Allowed;
    }
}
