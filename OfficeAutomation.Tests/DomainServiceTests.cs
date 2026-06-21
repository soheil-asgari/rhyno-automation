using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;
using Xunit;

namespace OfficeAutomation.Tests;

public sealed class DomainServiceTests
{
    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void LeaveWorkflowService_ReturnsExpectedNextStatus()
    {
        using var context = CreateContext(nameof(LeaveWorkflowService_ReturnsExpectedNextStatus));
        var service = new LeaveWorkflowService(context);

        Assert.Equal("در انتظار تایید مدیر واحد", service.GetNextStatus("ثبت اولیه", true));
        Assert.Equal("در انتظار منابع انسانی", service.GetNextStatus("در انتظار تایید مدیر واحد", true));
        Assert.Equal("تایید نهایی", service.GetNextStatus("در انتظار منابع انسانی", true));
        Assert.Equal("رد شده", service.GetNextStatus("ثبت اولیه", false));
    }

    [Fact]
    public async Task LeaveWorkflowService_ResolvesManagerFromDirectManagerId()
    {
        using var context = CreateContext(nameof(LeaveWorkflowService_ResolvesManagerFromDirectManagerId));
        context.Users.AddRange(
            new User { Id = "manager-1", UserName = "manager", FullName = "Manager One", DepartmentId = 10, IsManager = true },
            new User { Id = "user-1", UserName = "user", FullName = "User One", DepartmentId = 10, ManagerId = "manager-1" });
        await context.SaveChangesAsync();

        var service = new LeaveWorkflowService(context);

        var managerId = await service.GetManagerIdForUser("user-1");

        Assert.Equal("manager-1", managerId);
    }

    [Fact]
    public async Task PermissionAccessService_ReturnsPermissionsForRole()
    {
        using var context = CreateContext(nameof(PermissionAccessService_ReturnsPermissionsForRole));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var role = new ApplicationRole { Id = "role-1", Name = "Finance", DataAccessScope = RoleDataAccessScope.Department };
        var user = new User { Id = "user-1", UserName = "user", FullName = "Test User", DepartmentId = 20 };

        context.Roles.Add(role);
        context.Users.Add(user);
        context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = "Finance.View", IsAllowed = true });
        await context.SaveChangesAsync();

        var service = new PermissionAccessService(context, cache);

        var profile = await service.GetAccessProfileAsync(user.Id);

        Assert.NotNull(profile);
        Assert.Contains("Finance.View", profile!.Permissions);
        Assert.Equal("Test User", profile.DisplayName);
        Assert.Contains("Finance", profile.Roles);
    }

    [Fact]
    public async Task DataIsolationService_RestrictsByDepartmentWhenNoGlobalAccess()
    {
        using var context = CreateContext(nameof(DataIsolationService_RestrictsByDepartmentWhenNoGlobalAccess));
        context.Departments.AddRange(
            new Department { Id = 1, Name = "HR" },
            new Department { Id = 2, Name = "Finance" });
        context.Users.Add(new User { Id = "user-1", FullName = "User One", DepartmentId = 1 });
        await context.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var permissionService = new PermissionAccessService(context, cache);
        var profile = await permissionService.GetAccessProfileAsync("user-1");
        Assert.NotNull(profile);

        var accessor = new FixedCurrentUserContextAccessor(profile!);
        var isolationService = new DataIsolationService(accessor);

        var query = context.Users.AsQueryable();
        var scoped = await isolationService.ApplyDepartmentScopeAsync(query);
        var result = await scoped.ToListAsync();

        Assert.Single(result);
        Assert.Equal("user-1", result[0].Id);
    }

    [Fact]
    public async Task DataIsolationService_ReturnsEmptyQueryWhenNoProfile()
    {
        using var context = CreateContext(nameof(DataIsolationService_ReturnsEmptyQueryWhenNoProfile));
        context.Users.Add(new User { Id = "user-1", FullName = "User One", DepartmentId = 1 });
        await context.SaveChangesAsync();

        var isolationService = new DataIsolationService(new FixedCurrentUserContextAccessor(null));
        var scoped = await isolationService.ApplyDepartmentScopeAsync(context.Users.AsQueryable());
        var result = await scoped.ToListAsync();

        Assert.Empty(result);
    }

    private sealed class FixedCurrentUserContextAccessor : ICurrentUserContextAccessor
    {
        private readonly PermissionAccessProfile? _profile;

        public FixedCurrentUserContextAccessor(PermissionAccessProfile? profile)
        {
            _profile = profile;
        }

        public string? UserId => _profile?.UserId;

        public Task<PermissionAccessProfile?> GetAccessProfileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_profile);
        }
    }
}
