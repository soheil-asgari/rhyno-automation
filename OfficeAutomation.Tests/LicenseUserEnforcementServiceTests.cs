using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Services.Licensing;
using Xunit;

using ModularIdentityDbContext = OfficeAutomation.Modules.Identity.Infrastructure.Persistence.IdentityDbContext;

namespace OfficeAutomation.Tests;

public class LicenseUserEnforcementServiceTests
{
    private static ModularIdentityDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ModularIdentityDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;

        return new ModularIdentityDbContext(options);
    }

    private static (RoleManager<ApplicationRole> roleMgr, UserManager<User> userMgr) CreateIdentityManagers(ModularIdentityDbContext context)
    {
        var userStore = new UserStore<User, ApplicationRole, ModularIdentityDbContext, string>(context);
        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            new IUserValidator<User>[0],
            new IPasswordValidator<User>[0],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<User>>.Instance);

        var roleStore = new RoleStore<ApplicationRole, ModularIdentityDbContext, string>(context);
        var roleManager = new RoleManager<ApplicationRole>(
            roleStore,
            new IRoleValidator<ApplicationRole>[0],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);

        return (roleManager, userManager);
    }

    [Fact]
    public async Task ValidateUserCreation_WhenGlobalLimitReached_BlocksCreation()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var (roleMgr, userMgr) = CreateIdentityManagers(context);

        // Add 2 active users
        context.Users.Add(new User { Id = "u1", UserName = "u1@test.com", Email = "u1@test.com", FullName = "User 1" });
        context.Users.Add(new User { Id = "u2", UserName = "u2@test.com", Email = "u2@test.com", FullName = "User 2" });
        await context.SaveChangesAsync();

        var mockLicenseContext = new Mock<ILicenseContext>();
        var license = new LicenseInfo
        {
            CustomerName = "Test Corp",
            Modules = ["Contracts"],
            MaxUsers = 2
        };
        mockLicenseContext.Setup(c => c.IsLicensed).Returns(true);
        mockLicenseContext.Setup(c => c.IsLocked).Returns(false);
        mockLicenseContext.Setup(c => c.CurrentLicense).Returns(license);

        var service = new LicenseUserEnforcementService(mockLicenseContext.Object, context, roleMgr, userMgr);

        var result = await service.ValidateUserCreationOrRoleAssignmentAsync(
            existingUserId: null,
            targetRoleNames: ["SomeRole"]);

        Assert.False(result.Allowed);
        Assert.Contains("سقف مجاز کل کاربران سامانه", result.Reason);
        Assert.Equal(2, result.CurrentCount);
        Assert.Equal(2, result.MaxAllowed);
    }

    [Fact]
    public async Task ValidateUserRoleAssignment_WhenModuleLimitReached_BlocksAssignment()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var (roleMgr, userMgr) = CreateIdentityManagers(context);

        // Seed role for Contracts
        var contractRole = new ApplicationRole { Id = "r-contract", Name = "ContractOfficer" };
        await roleMgr.CreateAsync(contractRole);
        context.RolePermissions.Add(new RolePermission { RoleId = contractRole.Id, PermissionKey = "Contract.View", IsAllowed = true });

        // Add user 1 and assign to contract role
        var u1 = new User { Id = "u1", UserName = "u1@test.com", Email = "u1@test.com", FullName = "User 1" };
        await userMgr.CreateAsync(u1);
        await userMgr.AddToRoleAsync(u1, contractRole.Name);

        // Add user 2 (not yet assigned)
        var u2 = new User { Id = "u2", UserName = "u2@test.com", Email = "u2@test.com", FullName = "User 2" };
        await userMgr.CreateAsync(u2);
        await context.SaveChangesAsync();

        var mockLicenseContext = new Mock<ILicenseContext>();
        var license = new LicenseInfo
        {
            CustomerName = "Test Corp",
            Modules = ["Contracts"],
            MaxUsers = 10,
            ModuleUserLimits = new Dictionary<string, int> { ["Contracts"] = 1 }
        };
        mockLicenseContext.Setup(c => c.IsLicensed).Returns(true);
        mockLicenseContext.Setup(c => c.IsLocked).Returns(false);
        mockLicenseContext.Setup(c => c.CurrentLicense).Returns(license);

        var service = new LicenseUserEnforcementService(mockLicenseContext.Object, context, roleMgr, userMgr);

        // Assign u2 to ContractOfficer -> should be blocked because limit is 1 and u1 already has it
        var result = await service.ValidateUserCreationOrRoleAssignmentAsync(
            existingUserId: u2.Id,
            targetRoleNames: [contractRole.Name]);

        Assert.False(result.Allowed);
        Assert.Contains("شما به سقف مجاز کاربران این ماژول", result.Reason);
        Assert.Equal(1, result.CurrentCount);
        Assert.Equal(1, result.MaxAllowed);
    }

    [Fact]
    public async Task ValidateUserRoleAssignment_WhenUserAlreadyInModule_AllowsReassignment()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var (roleMgr, userMgr) = CreateIdentityManagers(context);

        var contractRole = new ApplicationRole { Id = "r-contract", Name = "ContractOfficer" };
        var contractAdminRole = new ApplicationRole { Id = "r-contract-admin", Name = "ContractAdmin" };
        await roleMgr.CreateAsync(contractRole);
        await roleMgr.CreateAsync(contractAdminRole);
        context.RolePermissions.Add(new RolePermission { RoleId = contractRole.Id, PermissionKey = "Contract.View", IsAllowed = true });
        context.RolePermissions.Add(new RolePermission { RoleId = contractAdminRole.Id, PermissionKey = "Contract.Admin", IsAllowed = true });

        var u1 = new User { Id = "u1", UserName = "u1@test.com", Email = "u1@test.com", FullName = "User 1" };
        await userMgr.CreateAsync(u1);
        await userMgr.AddToRoleAsync(u1, contractRole.Name);

        var mockLicenseContext = new Mock<ILicenseContext>();
        var license = new LicenseInfo
        {
            CustomerName = "Test Corp",
            Modules = ["Contracts"],
            MaxUsers = 10,
            ModuleUserLimits = new Dictionary<string, int> { ["Contracts"] = 1 }
        };
        mockLicenseContext.Setup(c => c.IsLicensed).Returns(true);
        mockLicenseContext.Setup(c => c.IsLocked).Returns(false);
        mockLicenseContext.Setup(c => c.CurrentLicense).Returns(license);

        var service = new LicenseUserEnforcementService(mockLicenseContext.Object, context, roleMgr, userMgr);

        // Changing/Adding ContractAdmin to u1 should be allowed because u1 is already counted in Contracts
        var result = await service.ValidateUserCreationOrRoleAssignmentAsync(
            existingUserId: u1.Id,
            targetRoleNames: [contractAdminRole.Name]);

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task DowngradeEdgeCase_ReportsExceededModules_WithoutDeletingUsers()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var (roleMgr, userMgr) = CreateIdentityManagers(context);

        var contractRole = new ApplicationRole { Id = "r-contract", Name = "ContractOfficer" };
        await roleMgr.CreateAsync(contractRole);
        context.RolePermissions.Add(new RolePermission { RoleId = contractRole.Id, PermissionKey = "Contract.View", IsAllowed = true });

        // 3 users in Contracts
        for (int i = 1; i <= 3; i++)
        {
            var u = new User { Id = $"u{i}", UserName = $"u{i}@test.com", Email = $"u{i}@test.com", FullName = $"User {i}" };
            await userMgr.CreateAsync(u);
            await userMgr.AddToRoleAsync(u, contractRole.Name);
        }

        // License was downgraded from 5 to 2
        var mockLicenseContext = new Mock<ILicenseContext>();
        var license = new LicenseInfo
        {
            CustomerName = "Test Corp",
            Modules = ["Contracts"],
            MaxUsers = 2,
            ModuleUserLimits = new Dictionary<string, int> { ["Contracts"] = 2 }
        };
        mockLicenseContext.Setup(c => c.IsLicensed).Returns(true);
        mockLicenseContext.Setup(c => c.IsLocked).Returns(false);
        mockLicenseContext.Setup(c => c.CurrentLicense).Returns(license);

        var service = new LicenseUserEnforcementService(mockLicenseContext.Object, context, roleMgr, userMgr);

        // Verify users still exist in database
        Assert.Equal(3, await context.Users.CountAsync());

        // Check exceeded modules
        var exceeded = await service.GetDowngradedExceededModulesAsync();
        Assert.NotEmpty(exceeded);
        var contractsExceeded = exceeded.FirstOrDefault(e => e.ModuleName == "Contracts");
        Assert.NotNull(contractsExceeded);
        Assert.Equal(3, contractsExceeded.ActiveUserCount);
        Assert.Equal(2, contractsExceeded.MaxUsersAllowed);
        Assert.True(contractsExceeded.IsExceeded);

        // Adding 4th user must be blocked
        var blockResult = await service.ValidateUserCreationOrRoleAssignmentAsync(
            existingUserId: null,
            targetRoleNames: [contractRole.Name]);

        Assert.False(blockResult.Allowed);
    }
}
