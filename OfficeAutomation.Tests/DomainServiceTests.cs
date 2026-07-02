using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OfficeAutomation.Controllers;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Application.SavedViews;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Connectors;
using OfficeAutomation.Services.Decisioning;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Services.Tenancy;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using ModularIdentityDbContext = OfficeAutomation.Modules.Identity.Infrastructure.Persistence.IdentityDbContext;

namespace OfficeAutomation.Tests;

public sealed class DomainServiceTests
{
    private static WorkflowDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new WorkflowDbContext(options);
    }

    private static FinanceDbContext CreateFinanceContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new FinanceDbContext(options);
    }

    
    private static OfficeDbContext CreateOfficeContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<OfficeDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new OfficeDbContext(options);
    }

    private static InventoryDbContext CreateInventoryContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new InventoryDbContext(options);
    }

    private static PlatformDbContext CreatePlatformContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new PlatformDbContext(options);
    }
    private static ModularIdentityDbContext CreateIdentityContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ModularIdentityDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new ModularIdentityDbContext(options);
    }

    [Fact]
    public void LeaveWorkflowService_ReturnsExpectedNextStatus()
    {
        using var context = CreateContext(nameof(LeaveWorkflowService_ReturnsExpectedNextStatus));
        var service = new LeaveWorkflowService(context, new WorkflowService());

        Assert.Equal("در انتظار تایید مدیر واحد", service.GetNextStatus("ثبت اولیه", true));
        Assert.Equal("در انتظار منابع انسانی", service.GetNextStatus("در انتظار تایید مدیر واحد", true));
        Assert.Equal("تایید نهایی", service.GetNextStatus("در انتظار منابع انسانی", true));
        Assert.Equal(WorkflowStatus.Rejected, service.GetNextStatus("Draft", false));
    }

    [Fact]
    public async Task LeaveWorkflowService_ResolvesManagerFromDirectManagerId()
    {
        using var context = CreateContext(nameof(LeaveWorkflowService_ResolvesManagerFromDirectManagerId));
        context.Users.AddRange(
            new User { Id = "manager-1", UserName = "manager", FullName = "Manager One", DepartmentId = 10, IsManager = true },
            new User { Id = "user-1", UserName = "user", FullName = "User One", DepartmentId = 10, ManagerId = "manager-1" });
        await context.SaveChangesAsync();

        var service = new LeaveWorkflowService(context, new WorkflowService());

        var managerId = await service.GetManagerIdForUser("user-1");

        Assert.Equal("manager-1", managerId);
    }

    [Fact]
    public async Task WorkflowService_StartRoutingUsesConfiguredFirstApprover()
    {
        using var context = CreateContext(nameof(WorkflowService_StartRoutingUsesConfiguredFirstApprover));
        context.WorkflowDefinitionVersions.Add(new WorkflowDefinitionVersion
        {
            DocumentType = "Letter",
            Version = 1,
            IsActive = true,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            StepDefinitions =
            [
                new WorkflowStepDefinition
                {
                    StepKey = "manager-approval",
                    StepOrder = 1,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    SlaHours = 8,
                    Rules =
                    [
                        new WorkflowRule
                        {
                            FieldName = "DocumentType",
                            Operator = "eq",
                            Value = "Letter",
                            AssigneeUserId = "approver-1"
                        }
                    ]
                }
            ]
        });
        await context.SaveChangesAsync();

        var service = new WorkflowService(context);

        var result = await service.StartRoutingAsync("Letter", "sender-1", "receiver-1");

        Assert.False(result.IsCompleted);
        Assert.Equal("approver-1", result.ReceiverId);
        Assert.Equal(1, result.StepNumber);
        Assert.Equal(WorkflowStatus.PendingApproval, result.Status);
    }

    [Fact]
    public async Task WorkflowService_AdvanceRoutingCompletesWhenNoNextStep()
    {
        using var context = CreateContext(nameof(WorkflowService_AdvanceRoutingCompletesWhenNoNextStep));
        var service = new WorkflowService(context);

        var result = await service.AdvanceRoutingAsync("Letter", 1, "approver-1", "final-1");

        Assert.True(result.IsCompleted);
        Assert.Equal("final-1", result.ReceiverId);
        Assert.Equal(1, result.StepNumber);
        Assert.Equal(WorkflowStatus.Approved, result.Status);
    }

    [Fact]
    public async Task WorkflowService_CreatesInstanceStepAndDecisionHistory()
    {
        using var context = CreateContext(nameof(WorkflowService_CreatesInstanceStepAndDecisionHistory));
        context.Users.AddRange(
            new User { Id = "sender-1", UserName = "sender", FullName = "Sender" },
            new User { Id = "approver-1", UserName = "approver", FullName = "Approver" },
            new User { Id = "final-1", UserName = "final", FullName = "Final" });
        context.WorkflowDefinitionVersions.Add(new WorkflowDefinitionVersion
        {
            DocumentType = "Letter",
            Version = 1,
            IsActive = true,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            StepDefinitions =
            [
                new WorkflowStepDefinition
                {
                    StepKey = "manager-approval",
                    StepOrder = 1,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    SlaHours = 8,
                    Rules =
                    [
                        new WorkflowRule
                        {
                            FieldName = "DocumentType",
                            Operator = "eq",
                            Value = "Letter",
                            AssigneeUserId = "approver-1"
                        }
                    ]
                }
            ]
        });
        await context.SaveChangesAsync();

        var service = new WorkflowService(context);
        await service.StartRoutingAsync("Letter", "sender-1", "final-1", documentId: 10);
        await service.AdvanceRoutingAsync("Letter", 1, "approver-1", "final-1", documentId: 10, decidedByUserId: "approver-1");

        var instance = await context.WorkflowInstances
            .Include(item => item.Steps)
            .Include(item => item.Decisions)
            .SingleAsync(item => item.DocumentType == "Letter" && item.DocumentId == 10);

        Assert.Equal(WorkflowStatus.Approved, instance.Status);
        Assert.Equal(1, instance.CurrentStepNumber);
        Assert.NotNull(instance.CompletedAt);
        Assert.Single(instance.Steps);
        Assert.Equal(WorkflowStatus.Approved, instance.Steps[0].Status);
        Assert.Single(instance.Decisions);
        Assert.Equal("approver-1", instance.Decisions[0].DecidedByUserId);
    }

    [Fact]
    public async Task WorkflowService_RunningInstanceUsesBoundDefinitionVersion()
    {
        using var context = CreateContext(nameof(WorkflowService_RunningInstanceUsesBoundDefinitionVersion));
        context.Users.AddRange(
            new User { Id = "sender-1", UserName = "sender", FullName = "Sender" },
            new User { Id = "approver-1", UserName = "approver1", FullName = "Approver One" },
            new User { Id = "approver-2", UserName = "approver2", FullName = "Approver Two" },
            new User { Id = "final-1", UserName = "final", FullName = "Final" });
        context.WorkflowDefinitionVersions.AddRange(
            new WorkflowDefinitionVersion
            {
                DocumentType = "Letter",
                Version = 1,
                IsActive = true,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-2),
                StepDefinitions =
                [
                    new WorkflowStepDefinition
                    {
                        StepKey = "approval-1",
                        StepOrder = 1,
                        AssignmentMode = WorkflowAssignmentMode.User,
                        Rules =
                        [
                            new WorkflowRule { FieldName = "DocumentType", Operator = "eq", Value = "Letter", AssigneeUserId = "approver-1", NextStepKey = "approval-2" }
                        ]
                    },
                    new WorkflowStepDefinition
                    {
                        StepKey = "approval-2",
                        StepOrder = 2,
                        AssignmentMode = WorkflowAssignmentMode.User,
                        Rules =
                        [
                            new WorkflowRule { FieldName = "DocumentType", Operator = "eq", Value = "Letter", AssigneeUserId = "approver-1" }
                        ]
                    }
                ]
            },
            new WorkflowDefinitionVersion
            {
                DocumentType = "Letter",
                Version = 2,
                IsActive = false,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
                StepDefinitions =
                [
                    new WorkflowStepDefinition
                    {
                        StepKey = "approval-1",
                        StepOrder = 1,
                        AssignmentMode = WorkflowAssignmentMode.User,
                        Rules =
                        [
                            new WorkflowRule { FieldName = "DocumentType", Operator = "eq", Value = "Letter", AssigneeUserId = "approver-2", NextStepKey = "approval-2" }
                        ]
                    },
                    new WorkflowStepDefinition
                    {
                        StepKey = "approval-2",
                        StepOrder = 2,
                        AssignmentMode = WorkflowAssignmentMode.User,
                        Rules =
                        [
                            new WorkflowRule { FieldName = "DocumentType", Operator = "eq", Value = "Letter", AssigneeUserId = "approver-2" }
                        ]
                    }
                ]
            });
        await context.SaveChangesAsync();

        var service = new WorkflowService(context);
        var started = await service.StartRoutingAsync("Letter", "sender-1", "final-1", 77);

        var version1 = await context.WorkflowDefinitionVersions.SingleAsync(item => item.DocumentType == "Letter" && item.Version == 1);
        var version2 = await context.WorkflowDefinitionVersions.SingleAsync(item => item.DocumentType == "Letter" && item.Version == 2);
        version1.IsActive = false;
        version2.IsActive = true;
        await context.SaveChangesAsync();

        var advanced = await service.AdvanceRoutingAsync("Letter", 1, started.ReceiverId, "final-1", 77, started.ReceiverId);
        var instance = await context.WorkflowInstances.SingleAsync(item => item.DocumentType == "Letter" && item.DocumentId == 77);

        Assert.False(advanced.IsCompleted);
        Assert.Equal("approver-1", advanced.ReceiverId);
        Assert.Equal(2, advanced.StepNumber);
        Assert.Equal(version1.Id, instance.DefinitionVersionId);
    }

    [Fact]
    public void WorkflowDecisionEngine_RegressionReportDetectsOutputChange()
    {
        var engine = new DecisionEngine();
        var table = new DecisionTableDefinition
        {
            TableKey = "Letter:approval:routing",
            VersionTag = "candidate-v2",
            Rules =
            [
                new DecisionRuleDefinition
                {
                    RuleId = "rule-1",
                    SortOrder = 1,
                    FieldName = "Amount",
                    Operator = ">=",
                    Value = "1000",
                    Outputs = new Dictionary<string, object?> { ["NextStepKey"] = "finance-review" }
                }
            ]
        };

        var report = engine.RunRegression(table,
        [
            new DecisionRegressionCase
            {
                CaseId = "case-1",
                Facts = new Dictionary<string, object?> { ["Amount"] = 1200 },
                ExpectedRuleId = "rule-1",
                ExpectedOutputs = new Dictionary<string, object?> { ["NextStepKey"] = "ceo-review" }
            }
        ]);

        Assert.Equal(1, report.TotalCases);
        Assert.Equal(1, report.FailedCases);
        Assert.False(report.Results[0].Passed);
    }

    [Fact]
    public async Task WorkflowService_ResolveNextStepIncludesExplainability()
    {
        using var context = CreateContext(nameof(WorkflowService_ResolveNextStepIncludesExplainability));
        context.WorkflowDefinitionVersions.Add(new WorkflowDefinitionVersion
        {
            DocumentType = "Letter",
            Version = 1,
            IsActive = true,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            StepDefinitions =
            [
                new WorkflowStepDefinition
                {
                    StepKey = "start",
                    StepOrder = 1,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    Rules =
                    [
                        new WorkflowRule
                        {
                            FieldName = "DocumentType",
                            Operator = "eq",
                            Value = "Letter",
                            AssigneeUserId = "approver-1",
                            NextStepKey = "final"
                        }
                    ]
                },
                new WorkflowStepDefinition
                {
                    StepKey = "final",
                    StepOrder = 2,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    Rules =
                    [
                        new WorkflowRule
                        {
                            FieldName = "DocumentType",
                            Operator = "eq",
                            Value = "Letter",
                            AssigneeUserId = "approver-2"
                        }
                    ]
                }
            ]
        });
        await context.SaveChangesAsync();

        var service = new WorkflowService(context);
        await service.StartRoutingAsync("Letter", "sender-1", "receiver-1", 91);

        var result = await service.ResolveNextStepAsync("Letter", 91, "start", "sender-1");

        Assert.NotNull(result);
        Assert.Equal(1, result!.DefinitionVersion);
        Assert.NotNull(result.Explanation);
        Assert.Equal("workflow.preview", result.Explanation!.DecisionContext);
        Assert.Equal("rule-1", result.Explanation.RoutingDecision!.MatchedRuleId);
        Assert.Equal("rule-2", result.Explanation.AssignmentDecision!.MatchedRuleId);
    }

    [Fact]
    public async Task WorkflowGovernanceService_SimulateAsync_ProjectsPathsTimersAndSla()
    {
        using var context = CreateContext(nameof(WorkflowGovernanceService_SimulateAsync_ProjectsPathsTimersAndSla));
        context.WorkflowDefinitionVersions.Add(new WorkflowDefinitionVersion
        {
            DocumentType = "Letter",
            Version = 3,
            IsActive = true,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            StepDefinitions =
            [
                new WorkflowStepDefinition
                {
                    StepKey = "draft-check",
                    StepOrder = 1,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    SlaHours = 4,
                    EscalationHours = 6,
                    Rules =
                    [
                        new WorkflowRule
                        {
                            FieldName = "Amount",
                            Operator = ">=",
                            Value = "1000",
                            NextStepKey = "finance-review",
                            AssigneeUserId = "finance-1"
                        }
                    ]
                },
                new WorkflowStepDefinition
                {
                    StepKey = "finance-review",
                    StepOrder = 2,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    SlaHours = 8,
                    EscalationHours = 12,
                    Rules =
                    [
                        new WorkflowRule
                        {
                            FieldName = "DocumentType",
                            Operator = "eq",
                            Value = "Letter",
                            AssigneeUserId = "approver-2"
                        }
                    ]
                }
            ]
        });
        await context.SaveChangesAsync();

        var definition = await context.WorkflowDefinitionVersions.SingleAsync();
        var service = new WorkflowGovernanceService(context, new DecisionEngine(), new WorkflowDefinitionSelector());

        var report = await service.SimulateAsync(
            "Letter",
            definition.Id,
            [
                new WorkflowSimulationScenario
                {
                    ScenarioId = "high-value",
                    Facts = new Dictionary<string, object?> { ["Amount"] = 2500, ["DocumentType"] = "Letter" }
                }
            ]);

        Assert.NotNull(report);
        Assert.Single(report!.Paths);
        Assert.Equal(12, report.Paths[0].TotalSlaHours);
        Assert.Equal(18, report.Paths[0].TotalEscalationHours);
        Assert.Equal(2, report.Paths[0].Steps.Count);
        Assert.Contains(report.Paths[0].Steps.SelectMany(item => item.TimerEvents), item => item.EventType == "sla.escalation" && item.OffsetHours == 12);
    }

    [Fact]
    public void WorkflowDefinitionSelector_CanaryRoutesSubsetOfNewInstances()
    {
        var selector = new WorkflowDefinitionSelector();
        var stable = new WorkflowDefinitionVersion
        {
            Id = 10,
            DocumentType = "Letter",
            Version = 1,
            IsActive = true,
            DeploymentMode = WorkflowDeploymentMode.Stable,
            TrafficPercentage = 100,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-5)
        };
        var canary = new WorkflowDefinitionVersion
        {
            Id = 11,
            DocumentType = "Letter",
            Version = 2,
            IsActive = true,
            DeploymentMode = WorkflowDeploymentMode.Canary,
            TrafficPercentage = 20,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var hits = Enumerable.Range(1, 200)
            .Count(i => selector.SelectVersion("Letter", [stable, canary], i, $"user-{i}")?.Id == canary.Id);

        Assert.InRange(hits, 15, 60);
    }

    [Fact]
    public async Task WorkflowGovernanceService_Rollback_PreservesRunningInstanceBinding()
    {
        using var context = CreateContext(nameof(WorkflowGovernanceService_Rollback_PreservesRunningInstanceBinding));
        context.Users.AddRange(
            new User { Id = "sender-1", UserName = "sender", FullName = "Sender" },
            new User { Id = "approver-1", UserName = "approver1", FullName = "Approver One" },
            new User { Id = "approver-2", UserName = "approver2", FullName = "Approver Two" });

        var stable = new WorkflowDefinitionVersion
        {
            DocumentType = "Letter",
            Version = 1,
            IsActive = true,
            DeploymentMode = WorkflowDeploymentMode.Stable,
            TrafficPercentage = 100,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-5),
            StepDefinitions =
            [
                new WorkflowStepDefinition
                {
                    StepKey = "review",
                    StepOrder = 1,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    Rules = [ new WorkflowRule { FieldName = "DocumentType", Operator = "eq", Value = "Letter", AssigneeUserId = "approver-1" } ]
                }
            ]
        };
        var candidate = new WorkflowDefinitionVersion
        {
            DocumentType = "Letter",
            Version = 2,
            IsActive = true,
            DeploymentMode = WorkflowDeploymentMode.Canary,
            TrafficPercentage = 100,
            RollbackOfVersionId = 1,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            StepDefinitions =
            [
                new WorkflowStepDefinition
                {
                    StepKey = "review",
                    StepOrder = 1,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    Rules = [ new WorkflowRule { FieldName = "DocumentType", Operator = "eq", Value = "Letter", AssigneeUserId = "approver-2" } ]
                }
            ]
        };
        context.WorkflowDefinitionVersions.AddRange(stable, candidate);
        await context.SaveChangesAsync();

        var service = new WorkflowService(context);
        var governance = new WorkflowGovernanceService(context, new DecisionEngine(), new WorkflowDefinitionSelector());
        await governance.DeployVersionAsync("Letter", candidate.Id, WorkflowDeploymentMode.Canary, 100, "green");

        var started = await service.StartRoutingAsync("Letter", "sender-1", "receiver-1", 501);
        var instance = await context.WorkflowInstances.SingleAsync(item => item.DocumentId == 501);

        var rollback = await governance.RollbackAsync("Letter", candidate.Id);

        Assert.NotNull(rollback);
        Assert.Equal(candidate.Id, instance.DefinitionVersionId);
        Assert.Equal("approver-2", started.ReceiverId);

        var nextStart = await service.StartRoutingAsync("Letter", "sender-1", "receiver-1", 502);
        Assert.Equal("approver-1", nextStart.ReceiverId);
    }

    [Fact]
    public async Task WorkflowService_StartDirectAssignmentCreatesPendingStep()
    {
        using var context = CreateContext(nameof(WorkflowService_StartDirectAssignmentCreatesPendingStep));
        context.Users.AddRange(
            new User { Id = "requester-1", UserName = "requester", FullName = "Requester" },
            new User { Id = "manager-1", UserName = "manager", FullName = "Manager" });
        await context.SaveChangesAsync();

        var service = new WorkflowService(context);
        await service.StartDirectAssignmentAsync("InventoryTransferRequest", 25, "requester-1", "manager-1");

        var instance = await context.WorkflowInstances
            .Include(item => item.Steps)
            .SingleAsync(item => item.DocumentType == "InventoryTransferRequest" && item.DocumentId == 25);

        Assert.Equal(WorkflowStatus.PendingApproval, instance.Status);
        Assert.Single(instance.Steps);
        Assert.Equal("manager-1", instance.Steps[0].AssignedToUserId);
    }

    [Fact]
    public async Task PermissionAccessService_ReturnsPermissionsForRole()
    {
        using var context = CreateIdentityContext(nameof(PermissionAccessService_ReturnsPermissionsForRole));
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
        using var context = CreateIdentityContext(nameof(DataIsolationService_RestrictsByDepartmentWhenNoGlobalAccess));
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
        var isolationService = new DataIsolationService(accessor, CreateScope(profile));

        var query = context.Users.AsQueryable();
        var scoped = await isolationService.ApplyDepartmentScopeAsync(query);
        var result = await scoped.ToListAsync();

        Assert.Single(result);
        Assert.Equal("user-1", result[0].Id);
    }

    [Fact]
    public async Task DataIsolationService_ReturnsEmptyQueryWhenNoProfile()
    {
        using var context = CreateIdentityContext(nameof(DataIsolationService_ReturnsEmptyQueryWhenNoProfile));
        context.Users.Add(new User { Id = "user-1", FullName = "User One", DepartmentId = 1 });
        await context.SaveChangesAsync();

        var isolationService = new DataIsolationService(new FixedCurrentUserContextAccessor(null), CreateScope(null));
        var scoped = await isolationService.ApplyDepartmentScopeAsync(context.Users.AsQueryable());
        var result = await scoped.ToListAsync();

        Assert.Empty(result);
    }

    [Fact]
    public void SensitiveControllers_RequireAuthenticatedUsers()
    {
        Assert.Contains(typeof(AiAssistantController).GetCustomAttributes(inherit: true), item => item is AuthorizeAttribute);
        Assert.Contains(typeof(WaybillController).GetCustomAttributes(inherit: true), item => item is AuthorizeAttribute);
    }

    [Fact]
    public void PermissionCatalog_MapsWaybillToWarehouseView()
    {
        Assert.True(PermissionCatalog.ControllerFallbackPermissions.TryGetValue("Waybill", out var permissions));
        Assert.Contains("Warehouse.View", permissions);
    }

    [Fact]
    public void FinancialInvoiceService_CalculatesVatAndTotals()
    {
        using var context = CreateFinanceContext(nameof(FinancialInvoiceService_CalculatesVatAndTotals));
        using var identityContext = CreateIdentityContext(nameof(FinancialInvoiceService_CalculatesVatAndTotals));
        var service = new FinancialInvoiceService(context, identityContext);

        var result = service.CalculateTotals(
        [
            new FinancialInvoiceItemVM { ItemName = "Service", Quantity = 2, UnitPrice = 100 },
            new FinancialInvoiceItemVM { ItemName = "Ignored", Quantity = 0, UnitPrice = 1000 }
        ]);

        Assert.Single(result.ValidItems);
        Assert.Equal(200, result.SubTotal);
        Assert.Equal(20, result.VatAmount);
        Assert.Equal(220, result.GrandTotal);
    }

    [Fact]
    public async Task FinancialInvoiceService_FiltersInvoiceIndexInDatabaseShape()
    {
        using var context = CreateFinanceContext(nameof(FinancialInvoiceService_FiltersInvoiceIndexInDatabaseShape));
        using var identityContext = CreateIdentityContext(nameof(FinancialInvoiceService_FiltersInvoiceIndexInDatabaseShape));
        context.Invoices.AddRange(
            new Invoice { InvoiceNumber = "S-1", InvoiceType = "Sale", DateShamsi = "1403/01/10", PartyName = "Alpha", VendorName = "Alpha", Amount = 100, InvoiceDate = DateTime.Today },
            new Invoice { InvoiceNumber = "P-1", InvoiceType = "Purchase", DateShamsi = "1403/01/10", PartyName = "Beta", VendorName = "Beta", Amount = 100, InvoiceDate = DateTime.Today });
        await context.SaveChangesAsync();

        var service = new FinancialInvoiceService(context, identityContext);
        var result = await service.BuildInvoiceIndexAsync(new FinancialInvoiceIndexVM { SearchTerm = "Alpha", Year = 1403 }, "Sale");

        Assert.Single(result.Items);
        Assert.Equal("S-1", result.Items[0].InvoiceNumber);
    }

    [Fact]
    public async Task FinancialInvoiceService_PublishesDecisionNotificationsOnce()
    {
        var databaseName = nameof(FinancialInvoiceService_PublishesDecisionNotificationsOnce);
        using var context = CreateFinanceContext(databaseName);
        using var identityContext = CreateIdentityContext(databaseName);
        using var legacyContext = CreateOfficeContext(databaseName);
        identityContext.Users.Add(new User { Id = "finance-1", UserName = "finance", FullName = "Finance User", CanAccessFinance = true });
        var invoice = new Invoice
        {
            Id = 1,
            InvoiceNumber = "P-1",
            InvoiceType = "Purchase",
            DateShamsi = "1403/01/10",
            PartyName = "Vendor",
            VendorName = "Vendor",
            Amount = 100,
            GrandTotal = 100,
            InvoiceDate = DateTime.Today
        };
        context.Invoices.Add(invoice);
        await identityContext.SaveChangesAsync();
        await context.SaveChangesAsync();

        var service = new FinancialInvoiceService(context, identityContext, new NotificationService(legacyContext));

        await service.PublishInvoiceDecisionNotificationAsync(invoice, WorkflowStatus.Approved, NotificationSeverity.Success);
        await service.PublishInvoiceDecisionNotificationAsync(invoice, WorkflowStatus.Approved, NotificationSeverity.Success);

        var notifications = await legacyContext.Notifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal("finance-1", notifications[0].RecipientUserId);
        Assert.Equal("InvoiceDecision", notifications[0].SourceEntityType);
    }

    [Fact]
    public async Task FinanceLedgerService_PostInvoice_UsesStructuredAccountsAndJournal()
    {
        using var context = CreateFinanceContext(nameof(FinanceLedgerService_PostInvoice_UsesStructuredAccountsAndJournal));
        var invoice = new Invoice
        {
            Id = 101,
            InvoiceNumber = "S-101",
            InvoiceType = "Sale",
            DateShamsi = "1403/01/10",
            PartyName = "Customer A",
            VendorName = "Customer A",
            SubTotal = 100,
            VatAmount = 10,
            GrandTotal = 110,
            Amount = 110,
            InvoiceDate = DateTime.Today
        };

        var service = new FinanceLedgerService(context);

        var voucher = await service.PostInvoiceAsync(invoice);

        var persisted = await context.VoucherHeaders
            .Include(item => item.JournalType)
            .Include(item => item.Lines)
            .ThenInclude(item => item.SubsidiaryAccount)
            .Include(item => item.Lines)
            .ThenInclude(item => item.FloatingDetailAccount)
            .SingleAsync(item => item.Id == voucher.Id);

        Assert.Equal(PostingStatus.Posted, persisted.PostingStatus);
        Assert.Equal(JournalTypeCodes.Sales, persisted.JournalType.Code);
        Assert.Equal(110, persisted.TotalDebits);
        Assert.Equal(110, persisted.TotalCredits);
        Assert.All(persisted.Lines, line => Assert.True(line.SubsidiaryAccountId > 0));
        Assert.Contains(persisted.Lines, line => line.SubsidiaryAccount.SystemKey == FinanceAccountKeys.AccountsReceivable && line.FloatingDetailAccount != null);
        Assert.Contains(persisted.Lines, line => line.CurrencyId == null && line.ExchangeRate == 1m);
    }

    [Fact]
    public async Task VoucherLine_CanReferenceFloatingDetailAccount_AndRemainBalanced()
    {
        using var context = CreateFinanceContext(nameof(VoucherLine_CanReferenceFloatingDetailAccount_AndRemainBalanced));
        var fiscalYear = new FiscalYear { YearName = "2030", StartDate = new DateTime(2030, 1, 1), EndDate = new DateTime(2030, 12, 31) };
        var journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal" };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var receivable = new SubsidiaryAccount { Code = "110301", Name = "Receivable", SystemKey = "AR_TEST", GeneralAccount = new GeneralAccount { Code = "110", Name = "Receivable", AccountGroup = assets } };
        var clearing = new SubsidiaryAccount { Code = "210301", Name = "Clearing", SystemKey = "CL_TEST", GeneralAccount = new GeneralAccount { Code = "210", Name = "Clearing", AccountGroup = liabilities }, AllowsFloatingDetail = false };
        var floating = new FloatingDetailAccount { Code = "CUST-2030", Name = "Customer 2030", Type = FloatingDetailAccountType.Person };

        context.AddRange(fiscalYear, journal, receivable, clearing, floating);
        context.SubsidiaryAccountFloatingDetails.Add(new SubsidiaryAccountFloatingDetail
        {
            SubsidiaryAccount = receivable,
            FloatingDetailAccount = floating
        });
        await context.SaveChangesAsync();

        context.VoucherHeaders.Add(new VoucherHeader
        {
            SequenceNumber = 1,
            VoucherNumber = 1,
            DocumentNumber = "FLOAT-1",
            VoucherDate = new DateTime(2030, 1, 15),
            Status = VoucherStatus.Draft,
            PostingStatus = PostingStatus.Draft,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = journal.Id,
            TotalDebits = 1_000m,
            TotalCredits = 1_000m,
            Lines =
            [
                new VoucherLine
                {
                    SubsidiaryAccountId = receivable.Id,
                    FloatingDetailAccountId = floating.Id,
                    DebitAmount = 1_000m,
                    CreditAmount = 0m,
                    ExchangeRate = 1m,
                    DisplayOrder = 1
                },
                new VoucherLine
                {
                    SubsidiaryAccountId = clearing.Id,
                    DebitAmount = 0m,
                    CreditAmount = 1_000m,
                    ExchangeRate = 1m,
                    DisplayOrder = 2
                }
            ]
        });

        await context.SaveChangesAsync();

        var line = await context.VoucherLines.Include(item => item.FloatingDetailAccount).SingleAsync(item => item.FloatingDetailAccountId == floating.Id);
        Assert.Equal(floating.Id, line.FloatingDetailAccountId);
        Assert.Equal(1_000m, line.DebitAmount);
        Assert.Equal(0m, line.CreditAmount);
    }

    [Fact]
    public async Task FinanceLedgerService_ReverseVoucher_CreatesBalancedOppositeVoucher()
    {
        using var context = CreateFinanceContext(nameof(FinanceLedgerService_ReverseVoucher_CreatesBalancedOppositeVoucher));
        var invoice = new Invoice
        {
            Id = 102,
            InvoiceNumber = "P-102",
            InvoiceType = "Purchase",
            DateShamsi = "1403/01/10",
            PartyName = "Vendor A",
            VendorName = "Vendor A",
            SubTotal = 200,
            VatAmount = 20,
            GrandTotal = 220,
            Amount = 220,
            InvoiceDate = DateTime.Today
        };
        var service = new FinanceLedgerService(context);
        var original = await service.PostInvoiceAsync(invoice);

        var reversal = await service.ReverseVoucherAsync(original.Id, "test reversal");

        var reversed = await context.VoucherHeaders
            .Include(item => item.Lines)
            .SingleAsync(item => item.Id == reversal.Id);

        Assert.Equal(original.Id, reversed.ReversalOfVoucherHeaderId);
        Assert.Equal(PostingStatus.Posted, reversed.PostingStatus);
        Assert.Equal(original.TotalDebits, reversed.TotalCredits);
        Assert.Equal(original.TotalCredits, reversed.TotalDebits);
        Assert.Equal(original.Lines.Count, reversed.Lines.Count);
    }

    [Fact]
    public async Task FinanceLedgerService_PostedVoucher_IsImmutable()
    {
        using var context = CreateFinanceContext(nameof(FinanceLedgerService_PostedVoucher_IsImmutable));
        var invoice = new Invoice
        {
            Id = 103,
            InvoiceNumber = "S-103",
            InvoiceType = "Sale",
            DateShamsi = "1403/01/10",
            PartyName = "Customer B",
            VendorName = "Customer B",
            SubTotal = 100,
            VatAmount = 10,
            GrandTotal = 110,
            Amount = 110,
            InvoiceDate = DateTime.Today
        };
        var service = new FinanceLedgerService(context);
        var voucher = await service.PostInvoiceAsync(invoice);

        voucher.Description = "illegal update";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        Assert.Contains("immutable", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinanceDbContext_PermanentVoucher_CannotBeEditedOrDeleted()
    {
        using var context = CreateFinanceContext(nameof(FinanceDbContext_PermanentVoucher_CannotBeEditedOrDeleted));
        var fiscalYear = new FiscalYear { YearName = "2031", StartDate = new DateTime(2031, 1, 1), EndDate = new DateTime(2031, 12, 31) };
        var journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal" };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var debitAccount = new SubsidiaryAccount { Code = "110401", Name = "Cash", SystemKey = "PERM_DR", GeneralAccount = new GeneralAccount { Code = "110", Name = "Cash", AccountGroup = assets } };
        var creditAccount = new SubsidiaryAccount { Code = "210401", Name = "Clearing", SystemKey = "PERM_CR", GeneralAccount = new GeneralAccount { Code = "210", Name = "Clearing", AccountGroup = liabilities } };

        context.AddRange(fiscalYear, journal, debitAccount, creditAccount);
        await context.SaveChangesAsync();

        var voucher = new VoucherHeader
        {
            SequenceNumber = 1,
            VoucherNumber = 1,
            DocumentNumber = "PERM-1",
            VoucherDate = new DateTime(2031, 1, 5),
            Status = VoucherStatus.Permanent,
            PostingStatus = PostingStatus.Draft,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = journal.Id,
            TotalDebits = 500m,
            TotalCredits = 500m,
            Lines =
            [
                new VoucherLine { SubsidiaryAccountId = debitAccount.Id, DebitAmount = 500m, ExchangeRate = 1m, DisplayOrder = 1 },
                new VoucherLine { SubsidiaryAccountId = creditAccount.Id, CreditAmount = 500m, ExchangeRate = 1m, DisplayOrder = 2 }
            ]
        };

        context.VoucherHeaders.Add(voucher);
        await context.SaveChangesAsync();

        voucher.Description = "illegal edit";
        var editError = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("Permanent", editError.Message, StringComparison.OrdinalIgnoreCase);

        context.Entry(voucher).State = EntityState.Unchanged;
        context.Remove(voucher);
        var deleteError = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("Permanent", deleteError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VoucherRenumberingService_RenumbersChronologicallyWithoutGaps()
    {
        using var context = CreateFinanceContext(nameof(VoucherRenumberingService_RenumbersChronologicallyWithoutGaps));
        var fiscalYear = new FiscalYear { YearName = "2032", StartDate = new DateTime(2032, 1, 1), EndDate = new DateTime(2032, 12, 31) };
        var period = new FiscalPeriod
        {
            FiscalYear = fiscalYear,
            Name = "2032-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2032, 1, 1),
            EndDate = new DateTime(2032, 1, 31),
            Status = FiscalPeriodStatus.Open
        };
        var journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal" };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var debitAccount = new SubsidiaryAccount { Code = "110501", Name = "Cash", SystemKey = "REN_DR", GeneralAccount = new GeneralAccount { Code = "110", Name = "Cash", AccountGroup = assets } };
        var creditAccount = new SubsidiaryAccount { Code = "210501", Name = "Clearing", SystemKey = "REN_CR", GeneralAccount = new GeneralAccount { Code = "210", Name = "Clearing", AccountGroup = liabilities } };

        context.AddRange(fiscalYear, period, journal, debitAccount, creditAccount);
        await context.SaveChangesAsync();

        context.VoucherHeaders.AddRange(
            CreateVoucher("REN-3", new DateTime(2032, 1, 20), 3, 99, VoucherStatus.Draft),
            CreateVoucher("REN-1", new DateTime(2032, 1, 10), 1, 42, VoucherStatus.Reviewed),
            CreateVoucher("REN-2", new DateTime(2032, 1, 10), 2, 77, VoucherStatus.Approved),
            CreateVoucher("REN-P", new DateTime(2032, 1, 12), 4, 500, VoucherStatus.Permanent));
        await context.SaveChangesAsync();

        var service = new VoucherRenumberingService(context);
        var affected = await service.RenumberAsync(period.Id);

        Assert.Equal(3, affected);

        var vouchers = await context.VoucherHeaders
            .AsNoTracking()
            .OrderBy(item => item.SequenceNumber)
            .ToListAsync();

        Assert.Equal(1, vouchers.Single(item => item.DocumentNumber == "REN-1").VoucherNumber);
        Assert.Equal(2, vouchers.Single(item => item.DocumentNumber == "REN-2").VoucherNumber);
        Assert.Equal(3, vouchers.Single(item => item.DocumentNumber == "REN-3").VoucherNumber);
        Assert.Equal(500, vouchers.Single(item => item.DocumentNumber == "REN-P").VoucherNumber);

        VoucherHeader CreateVoucher(string documentNumber, DateTime voucherDate, int sequenceNumber, int voucherNumber, VoucherStatus status)
        {
            return new VoucherHeader
            {
                SequenceNumber = sequenceNumber,
                VoucherNumber = voucherNumber,
                DocumentNumber = documentNumber,
                VoucherDate = voucherDate,
                Status = status,
                PostingStatus = PostingStatus.Draft,
                FiscalYearId = fiscalYear.Id,
                JournalTypeId = journal.Id,
                TotalDebits = 100m,
                TotalCredits = 100m,
                Lines =
                [
                    new VoucherLine { SubsidiaryAccountId = debitAccount.Id, DebitAmount = 100m, ExchangeRate = 1m, DisplayOrder = 1 },
                    new VoucherLine { SubsidiaryAccountId = creditAccount.Id, CreditAmount = 100m, ExchangeRate = 1m, DisplayOrder = 2 }
                ]
            };
        }
    }

    [Fact]
    public async Task FinanceLedgerService_UnbalancedVoucher_CannotMoveToReviewed()
    {
        using var context = CreateFinanceContext(nameof(FinanceLedgerService_UnbalancedVoucher_CannotMoveToReviewed));
        var fiscalYear = new FiscalYear { YearName = "2033", StartDate = new DateTime(2033, 1, 1), EndDate = new DateTime(2033, 12, 31) };
        var journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal" };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var debitAccount = new SubsidiaryAccount { Code = "110601", Name = "Cash", SystemKey = "WF_DR", GeneralAccount = new GeneralAccount { Code = "110", Name = "Cash", AccountGroup = assets } };
        var creditAccount = new SubsidiaryAccount { Code = "210601", Name = "Clearing", SystemKey = "WF_CR", GeneralAccount = new GeneralAccount { Code = "210", Name = "Clearing", AccountGroup = liabilities } };

        context.AddRange(fiscalYear, journal, debitAccount, creditAccount);
        await context.SaveChangesAsync();

        var voucher = new VoucherHeader
        {
            SequenceNumber = 1,
            VoucherNumber = 1,
            DocumentNumber = "WF-UNBAL-1",
            VoucherDate = new DateTime(2033, 1, 2),
            Status = VoucherStatus.Draft,
            PostingStatus = PostingStatus.Draft,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = journal.Id,
            TotalDebits = 100m,
            TotalCredits = 80m,
            Lines =
            [
                new VoucherLine { SubsidiaryAccountId = debitAccount.Id, DebitAmount = 100m, ExchangeRate = 1m, DisplayOrder = 1 },
                new VoucherLine { SubsidiaryAccountId = creditAccount.Id, CreditAmount = 80m, ExchangeRate = 1m, DisplayOrder = 2 }
            ]
        };

        context.VoucherHeaders.Add(voucher);
        await context.SaveChangesAsync();

        var service = new FinanceLedgerService(context);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeVoucherStatusAsync(voucher.Id, VoucherStatus.Reviewed));

        Assert.Contains("Unbalanced", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinanceLedgerService_PermanentVoucher_CannotRollbackStatus()
    {
        using var context = CreateFinanceContext(nameof(FinanceLedgerService_PermanentVoucher_CannotRollbackStatus));
        var fiscalYear = new FiscalYear { YearName = "2034", StartDate = new DateTime(2034, 1, 1), EndDate = new DateTime(2034, 12, 31) };
        var journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal" };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var debitAccount = new SubsidiaryAccount { Code = "110701", Name = "Cash", SystemKey = "WF_PERM_DR", GeneralAccount = new GeneralAccount { Code = "110", Name = "Cash", AccountGroup = assets } };
        var creditAccount = new SubsidiaryAccount { Code = "210701", Name = "Clearing", SystemKey = "WF_PERM_CR", GeneralAccount = new GeneralAccount { Code = "210", Name = "Clearing", AccountGroup = liabilities } };

        context.AddRange(fiscalYear, journal, debitAccount, creditAccount);
        await context.SaveChangesAsync();

        var voucher = new VoucherHeader
        {
            SequenceNumber = 1,
            VoucherNumber = 1,
            DocumentNumber = "WF-PERM-1",
            VoucherDate = new DateTime(2034, 1, 2),
            Status = VoucherStatus.Permanent,
            PostingStatus = PostingStatus.Draft,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = journal.Id,
            TotalDebits = 100m,
            TotalCredits = 100m,
            Lines =
            [
                new VoucherLine { SubsidiaryAccountId = debitAccount.Id, DebitAmount = 100m, ExchangeRate = 1m, DisplayOrder = 1 },
                new VoucherLine { SubsidiaryAccountId = creditAccount.Id, CreditAmount = 100m, ExchangeRate = 1m, DisplayOrder = 2 }
            ]
        };

        context.VoucherHeaders.Add(voucher);
        await context.SaveChangesAsync();

        var service = new FinanceLedgerService(context);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeVoucherStatusAsync(voucher.Id, VoucherStatus.Approved));

        Assert.Contains("Permanent", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinanceDbContext_BlocksVoucherWritesInLockedFiscalPeriod()
    {
        using var context = CreateFinanceContext(nameof(FinanceDbContext_BlocksVoucherWritesInLockedFiscalPeriod));
        var fiscalYear = new FiscalYear
        {
            YearName = "2026",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        };
        context.FiscalYears.Add(fiscalYear);
        context.FiscalPeriods.Add(new FiscalPeriod
        {
            FiscalYear = fiscalYear,
            Name = "2026-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            Status = FiscalPeriodStatus.SoftLocked
        });
        await context.SaveChangesAsync();

        var service = new FinanceLedgerService(context);
        var invoice = new Invoice
        {
            Id = 104,
            InvoiceNumber = "S-104",
            InvoiceType = "Sale",
            DateShamsi = "1404/10/25",
            PartyName = "Locked Customer",
            VendorName = "Locked Customer",
            SubTotal = 100,
            VatAmount = 0,
            GrandTotal = 100,
            Amount = 100,
            InvoiceDate = new DateTime(2026, 1, 15)
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostInvoiceAsync(invoice));

        Assert.Contains("Fiscal period", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SoftLocked", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinanceLedgerService_CloseTemporaryAccounts_CreatesBalancedClosingVoucher()
    {
        using var context = CreateFinanceContext(nameof(FinanceLedgerService_CloseTemporaryAccounts_CreatesBalancedClosingVoucher));
        var fiscalYear = new FiscalYear
        {
            YearName = "2027",
            StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 12, 31)
        };
        var period = new FiscalPeriod
        {
            FiscalYear = fiscalYear,
            Name = "2027-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 1, 31),
            Status = FiscalPeriodStatus.Open
        };
        context.FiscalYears.Add(fiscalYear);
        context.FiscalPeriods.Add(period);
        await context.SaveChangesAsync();

        var service = new FinanceLedgerService(context);
        await service.PostInvoiceAsync(new Invoice
        {
            Id = 105,
            InvoiceNumber = "S-105",
            InvoiceType = "Sale",
            DateShamsi = "1405/10/11",
            PartyName = "Closing Customer",
            VendorName = "Closing Customer",
            SubTotal = 100,
            VatAmount = 0,
            GrandTotal = 100,
            Amount = 100,
            InvoiceDate = new DateTime(2027, 1, 10)
        });
        await service.PostInvoiceAsync(new Invoice
        {
            Id = 106,
            InvoiceNumber = "P-106",
            InvoiceType = "Purchase",
            DateShamsi = "1405/10/12",
            PartyName = "Closing Vendor",
            VendorName = "Closing Vendor",
            SubTotal = 30,
            VatAmount = 0,
            GrandTotal = 30,
            Amount = 30,
            InvoiceDate = new DateTime(2027, 1, 11)
        });

        var closing = await service.CloseTemporaryAccountsAsync(period.Id);
        var persisted = await context.VoucherHeaders
            .Include(item => item.JournalType)
            .Include(item => item.Lines)
            .ThenInclude(item => item.SubsidiaryAccount)
            .SingleAsync(item => item.Id == closing.Id);

        Assert.Equal(JournalTypeCodes.Closing, persisted.JournalType.Code);
        Assert.Equal(persisted.TotalDebits, persisted.TotalCredits);
        Assert.Equal(100, persisted.TotalDebits);
        Assert.Contains(persisted.Lines, line => line.SubsidiaryAccount.SystemKey == FinanceAccountKeys.SalesRevenue && line.DebitAmount == 100);
        Assert.Contains(persisted.Lines, line => line.SubsidiaryAccount.SystemKey == FinanceAccountKeys.PurchaseExpense && line.CreditAmount == 30);
        Assert.Contains(persisted.Lines, line => line.SubsidiaryAccount.SystemKey == FinanceAccountKeys.RetainedEarnings && line.CreditAmount == 70);
    }

    [Fact]
    public async Task FinanceDbContext_ForeignCurrencyLine_ComputesBaseAmount()
    {
        using var context = CreateFinanceContext(nameof(FinanceDbContext_ForeignCurrencyLine_ComputesBaseAmount));
        var currency = new Currency { Code = "USD", Name = "US Dollar", Symbol = "$" };
        var fiscalYear = new FiscalYear { YearName = "2028", StartDate = new DateTime(2028, 1, 1), EndDate = new DateTime(2028, 12, 31) };
        var journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal" };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var bank = new SubsidiaryAccount { Code = "110201", Name = "USD Bank", SystemKey = "USD_BANK", GeneralAccount = new GeneralAccount { Code = "110", Name = "Cash", AccountGroup = assets } };
        var clearing = new SubsidiaryAccount { Code = "210201", Name = "Clearing", SystemKey = "CLEARING", GeneralAccount = new GeneralAccount { Code = "210", Name = "Clearing", AccountGroup = liabilities } };

        context.AddRange(currency, fiscalYear, journal, bank, clearing);
        await context.SaveChangesAsync();

        context.VoucherHeaders.Add(new VoucherHeader
        {
            SequenceNumber = 1,
            VoucherNumber = 1,
            DocumentNumber = "FX-LINE-1",
            VoucherDate = new DateTime(2028, 1, 10),
            Status = VoucherStatus.Permanent,
            PostingStatus = PostingStatus.Posted,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = journal.Id,
            TotalDebits = 4_200_000m,
            TotalCredits = 4_200_000m,
            Lines =
            [
                new VoucherLine
                {
                    SubsidiaryAccountId = bank.Id,
                    CurrencyId = currency.Id,
                    ForeignAmount = 100m,
                    ExchangeRate = 42_000m,
                    DebitAmount = 1m,
                    CreditAmount = 0m
                },
                new VoucherLine
                {
                    SubsidiaryAccountId = clearing.Id,
                    DebitAmount = 0m,
                    CreditAmount = 4_200_000m
                }
            ]
        });

        await context.SaveChangesAsync();

        var line = await context.VoucherLines.SingleAsync(item => item.CurrencyId == currency.Id);
        Assert.Equal(4_200_000m, line.DebitAmount);
        Assert.Equal(0m, line.CreditAmount);
        Assert.Equal(42_000m, line.ExchangeRate);
    }

    [Fact]
    public async Task FinanceLedgerService_RevalueForeignCurrencies_CreatesBalancedAdjustmentVoucher()
    {
        using var context = CreateFinanceContext(nameof(FinanceLedgerService_RevalueForeignCurrencies_CreatesBalancedAdjustmentVoucher));
        var gainLossAccountKey = Guid.NewGuid();
        var currency = new Currency { Code = "USD", Name = "US Dollar", Symbol = "$" };
        var fiscalYear = new FiscalYear { YearName = "2029", StartDate = new DateTime(2029, 1, 1), EndDate = new DateTime(2029, 12, 31) };
        var period = new FiscalPeriod
        {
            FiscalYear = fiscalYear,
            Name = "2029-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2029, 1, 1),
            EndDate = new DateTime(2029, 1, 31)
        };
        var journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal" };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var revenue = new AccountGroup { Code = "4", Name = "Revenue", Nature = AccountNature.Credit };
        var bank = new SubsidiaryAccount { Code = "110202", Name = "USD Bank", SystemKey = "USD_BANK", GeneralAccount = new GeneralAccount { Code = "110", Name = "Cash", AccountGroup = assets } };
        var clearing = new SubsidiaryAccount { Code = "210202", Name = "Clearing", SystemKey = "CLEARING", GeneralAccount = new GeneralAccount { Code = "210", Name = "Clearing", AccountGroup = liabilities } };
        var gainLoss = new SubsidiaryAccount { Code = "420201", Name = "FX Gain Loss", SystemKey = gainLossAccountKey.ToString(), GeneralAccount = new GeneralAccount { Code = "420", Name = "FX Gain Loss", AccountGroup = revenue } };

        context.AddRange(currency, fiscalYear, period, journal, bank, clearing, gainLoss);
        await context.SaveChangesAsync();

        context.VoucherHeaders.Add(new VoucherHeader
        {
            SequenceNumber = 1,
            VoucherNumber = 1,
            DocumentNumber = "FX-OPEN-1",
            VoucherDate = new DateTime(2029, 1, 10),
            Status = VoucherStatus.Permanent,
            PostingStatus = PostingStatus.Posted,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = journal.Id,
            TotalDebits = 4_000_000m,
            TotalCredits = 4_000_000m,
            Lines =
            [
                new VoucherLine
                {
                    SubsidiaryAccountId = bank.Id,
                    CurrencyId = currency.Id,
                    ForeignAmount = 100m,
                    ExchangeRate = 40_000m,
                    DebitAmount = 4_000_000m
                },
                new VoucherLine
                {
                    SubsidiaryAccountId = clearing.Id,
                    CreditAmount = 4_000_000m
                }
            ]
        });
        await context.SaveChangesAsync();

        var service = new FinanceLedgerService(context);
        var voucher = await service.RevalueForeignCurrenciesAsync(period.Id, currency.Id, 45_000m, gainLossAccountKey);
        var persisted = await context.VoucherHeaders
            .Include(item => item.JournalType)
            .Include(item => item.Lines)
            .ThenInclude(item => item.SubsidiaryAccount)
            .SingleAsync(item => item.Id == voucher.Id);

        Assert.Equal(JournalTypeCodes.Adjustment, persisted.JournalType.Code);
        Assert.Equal(500_000m, persisted.TotalDebits);
        Assert.Equal(500_000m, persisted.TotalCredits);
        Assert.Contains(persisted.Lines, line => line.SubsidiaryAccountId == bank.Id && line.DebitAmount == 500_000m && line.CurrencyId == currency.Id);
        Assert.Contains(persisted.Lines, line => line.SubsidiaryAccountId == gainLoss.Id && line.CreditAmount == 500_000m);
    }

    [Fact]
    public async Task WarehouseDashboardService_FiltersTransferRequestsBeforeMaterialization()
    {
        var databaseName = nameof(WarehouseDashboardService_FiltersTransferRequestsBeforeMaterialization);
        using var context = CreateInventoryContext(databaseName);
        using var identityContext = CreateIdentityContext(databaseName);
        using var officeContext = CreateOfficeContext(databaseName);
        context.Warehouses.AddRange(
            new Warehouse { Id = 1, Code = "W1", Name = "Main" },
            new Warehouse { Id = 2, Code = "W2", Name = "Reserve" });
        context.Products.AddRange(
            new Product { Id = 1, Code = "P-1", Name = "Bolt", Unit = "ط¹ط¯ط¯", MinimumStock = 10 },
            new Product { Id = 2, Code = "P-2", Name = "Nut", Unit = "ط¹ط¯ط¯", MinimumStock = 10 });
        identityContext.Users.AddRange(
            new User { Id = "1", UserName = "requester-1", FullName = "Requester One" },
            new User { Id = "2", UserName = "requester-2", FullName = "Requester Two" });
        context.InventoryTransferRequests.AddRange(
            new InventoryTransferRequest { SourceWarehouseId = 1, DestinationWarehouseId = 2, ProductId = 1, Quantity = 5, Status = WorkflowStatus.PendingApproval, RequestedByUserId = "1", CreatedAt = DateTime.Today },
            new InventoryTransferRequest { SourceWarehouseId = 2, DestinationWarehouseId = 1, ProductId = 2, Quantity = 7, Status = WorkflowStatus.Approved, RequestedByUserId = "2", CreatedAt = DateTime.Today });
        await identityContext.SaveChangesAsync();
        await context.SaveChangesAsync();

        var service = new WarehouseDashboardService(context, identityContext, new NotificationService(officeContext));
        var result = await service.GetTransferRequestsAsync(null, null, 1, null, 1, null, null, null);

        Assert.Single(result);
        Assert.Equal(1, result[0].ProductId);
    }

    [Fact]
    public void AiSqlSafetyService_RejectsMutatingSql()
    {
        var service = new AiSqlSafetyService();

        Assert.True(service.IsReadOnlySelect("SELECT TOP 10 * FROM Invoices"));
        Assert.True(service.IsReadOnlySelect("WITH x AS (SELECT 1 AS Id) SELECT * FROM x"));
        Assert.False(service.IsReadOnlySelect("SELECT * FROM Users; DROP TABLE Users"));
        Assert.False(service.IsReadOnlySelect("UPDATE Invoices SET Amount = 0"));
    }

    [Fact]
    public async Task NotificationService_ReturnsOnlyActiveUnreadHeaderNotifications()
    {
        using var context = CreateOfficeContext(nameof(NotificationService_ReturnsOnlyActiveUnreadHeaderNotifications));
        var service = new NotificationService(context);

        await service.CreateAsync("user-1", "Active", "Visible", NotificationSeverity.Warning, "/Letters");
        await service.CreateAsync("user-1", "Expired", "Hidden", NotificationSeverity.Info, "/Leaves", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await service.CreateAsync("user-2", "Other", "Hidden", NotificationSeverity.Info, "/Leaves");

        var header = await service.GetHeaderNotificationsAsync("user-1");

        Assert.Single(header);
        Assert.Equal("Active", header[0].Title);
        Assert.Equal("warning", header[0].Tone);
    }

    [Fact]
    public async Task NotificationService_MarksNotificationsReadForOwnerOnly()
    {
        using var context = CreateOfficeContext(nameof(NotificationService_MarksNotificationsReadForOwnerOnly));
        var service = new NotificationService(context);

        var notification = await service.CreateAsync("user-1", "Task", "Message");

        await service.MarkReadAsync(notification.Id, "user-2");
        Assert.False((await context.Notifications.FindAsync(notification.Id))!.IsRead);

        await service.MarkReadAsync(notification.Id, "user-1");
        Assert.True((await context.Notifications.FindAsync(notification.Id))!.IsRead);
    }

    [Fact]
    public async Task SecurityAuditNotificationService_PublishesSensitiveEventsOnce()
    {
        var databaseName = nameof(SecurityAuditNotificationService_PublishesSensitiveEventsOnce);
        using var context = CreatePlatformContext(databaseName);
        using var identityContext = CreateIdentityContext(databaseName);
        using var officeContext = CreateOfficeContext(databaseName);
        identityContext.Users.Add(new User { Id = "security-1", UserName = "security", FullName = "Security User" });
        identityContext.Roles.Add(new ApplicationRole { Id = "role-security", Name = "Security" });
        identityContext.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = "security-1", RoleId = "role-security" });
        identityContext.RolePermissions.Add(new RolePermission { RoleId = "role-security", PermissionKey = "AuditLogs.Read", IsAllowed = true });
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Action = "Delete",
            TableName = "RolePermissions",
            UserId = "actor-1",
            DateTime = DateTimeOffset.UtcNow,
            IsSensitive = true
        });
        await identityContext.SaveChangesAsync();
        await context.SaveChangesAsync();

        var service = new SecurityAuditNotificationService(context, identityContext, new NotificationService(officeContext));

        await service.PublishRecentSensitiveEventsAsync();
        await service.PublishRecentSensitiveEventsAsync();

        var notifications = await officeContext.Notifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal("security-1", notifications[0].RecipientUserId);
        Assert.Equal("Security", notifications[0].SourceModule);
        Assert.Equal("AuditLog", notifications[0].SourceEntityType);
    }

    [Fact]
    public async Task WorkInboxService_CombinesUnreadLettersAndApprovals()
    {
        using var context = CreateContext(nameof(WorkInboxService_CombinesUnreadLettersAndApprovals));
        context.Users.AddRange(
            new User { Id = "user-1", UserName = "user-1", FullName = "User One" },
            new User { Id = "sender-1", UserName = "sender-1", FullName = "Sender One" });
        context.Letters.Add(new Letter
        {
            Id = 10,
            Title = "Letter A",
            Body = "Body",
            SenderId = "sender-1",
            ReceiverId = "user-1",
            FinalReceiverId = "user-1",
            WorkflowStatus = WorkflowStatus.PendingApproval,
            CurrentWorkflowStep = 1,
            IsRead = false,
            SentDate = DateTime.Today
        });
        context.WorkflowInstances.Add(new WorkflowInstance
        {
            DocumentType = "Letter",
            DocumentId = 10,
            Status = WorkflowStatus.PendingApproval,
            CurrentStatus = WorkflowStatus.PendingApproval,
            CurrentStepNumber = 1,
            StartedByUserId = "sender-1",
            CurrentAssigneeUserId = "user-1",
            Steps =
            [
                new WorkflowStep
                {
                    StepNumber = 1,
                    AssignedToUserId = "user-1",
                    Status = WorkflowStatus.PendingApproval
                }
            ]
        });
        await context.SaveChangesAsync();

        using var identityContext = CreateIdentityContext(nameof(WorkInboxService_CombinesUnreadLettersAndApprovals));
        var service = new WorkInboxService(context, identityContext);
        var result = await service.BuildAsync("user-1", false, null);

        Assert.True(result.TotalCount >= 2);
        Assert.Contains(result.Items, item => item.Id == "letter-unread-10");
        Assert.Contains(result.Items, item => item.Id.StartsWith("workflow-step-") && item.Module == "Letter");
        Assert.True(result.ApprovalCount >= 1);
        Assert.True(result.LetterCount >= 1);
    }

    [Fact]
    public async Task WorkInboxService_IncludesAssignedWorkflowSteps()
    {
        using var context = CreateContext(nameof(WorkInboxService_IncludesAssignedWorkflowSteps));
        context.Users.AddRange(
            new User { Id = "sender-1", UserName = "sender", FullName = "Sender" },
            new User { Id = "approver-1", UserName = "approver", FullName = "Approver" });
        context.WorkflowInstances.Add(new WorkflowInstance
        {
            DocumentType = "Letter",
            DocumentId = 42,
            Status = WorkflowStatus.PendingApproval,
            CurrentStepNumber = 1,
            StartedByUserId = "sender-1",
            Steps =
            [
                new WorkflowStep
                {
                    StepNumber = 1,
                    AssignedToUserId = "approver-1",
                    Status = WorkflowStatus.PendingApproval
                }
            ]
        });
        await context.SaveChangesAsync();

        using var identityContext = CreateIdentityContext(nameof(WorkInboxService_IncludesAssignedWorkflowSteps));
        var service = new WorkInboxService(context, identityContext);
        var result = await service.BuildAsync("approver-1", false, "Approvals");

        var item = Assert.Single(result.Items, item => item.Id.StartsWith("workflow-step-"));
        Assert.Equal("/Letters/Details/42", item.Url);
        Assert.True(item.RequiresAction);
    }

    [Fact]
    public async Task WorkflowService_CreateWorkflow_CreatesInstanceAndFirstStep()
    {
        using var context = CreateContext(nameof(WorkflowService_CreateWorkflow_CreatesInstanceAndFirstStep));
        context.Users.AddRange(
            new User { Id = "sender-1", UserName = "sender", FullName = "Sender" },
            new User { Id = "approver-1", UserName = "approver", FullName = "Approver" });
        context.WorkflowDefinitionVersions.Add(new WorkflowDefinitionVersion
        {
            DocumentType = "Letter",
            Version = 1,
            IsActive = true,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            StepDefinitions =
            [
                new WorkflowStepDefinition
                {
                    StepKey = "Manager approval",
                    StepOrder = 1,
                    AssignmentMode = WorkflowAssignmentMode.User,
                    SlaHours = 8,
                    Rules =
                    [
                        new WorkflowRule
                        {
                            FieldName = "DocumentType",
                            Operator = "eq",
                            Value = "Letter",
                            AssigneeUserId = "approver-1"
                        }
                    ]
                }
            ]
        });
        await context.SaveChangesAsync();

        var service = new WorkflowService(context);
        await service.StartRoutingAsync("Letter", "sender-1", "sender-1", documentId: 501);

        var instance = await context.WorkflowInstances.Include(item => item.Steps).SingleAsync(item => item.DocumentId == 501);
        Assert.Equal(WorkflowStatus.PendingApproval, instance.Status);
        Assert.Equal("approver-1", instance.CurrentAssigneeUserId);
        var step = Assert.Single(instance.Steps);
        Assert.Equal("Manager approval", step.StepName);
        Assert.Equal("approver-1", step.AssignedToUserId);
    }

    [Fact]
    public async Task WorkflowService_AssignStep_CreatesDirectAssignment()
    {
        using var context = CreateContext(nameof(WorkflowService_AssignStep_CreatesDirectAssignment));
        context.Users.AddRange(
            new User { Id = "requester-1", UserName = "requester", FullName = "Requester" },
            new User { Id = "assignee-1", UserName = "assignee", FullName = "Assignee" });
        await context.SaveChangesAsync();

        var service = new WorkflowService(context);
        await service.StartDirectAssignmentAsync("Invoice", 710, "requester-1", "assignee-1");

        var step = await context.WorkflowSteps.Include(item => item.WorkflowInstance).SingleAsync();
        Assert.Equal("assignee-1", step.AssignedToUserId);
        Assert.Equal(WorkflowStatus.PendingApproval, step.Status);
        Assert.Equal("Invoice", step.WorkflowInstance!.DocumentType);
    }

    [Fact]
    public async Task WorkflowService_Approve_CompletesWorkflowAndLogsDecision()
    {
        using var context = CreateContext(nameof(WorkflowService_Approve_CompletesWorkflowAndLogsDecision));
        await SeedOpenWorkflowAsync(context, "Letter", 801, "sender-1", "approver-1");
        var service = new WorkflowService(context);

        var succeeded = await service.ExecuteDecisionAsync("Letter", 801, 1, "approver-1", WorkflowDecisionType.Approve, "ok", null, null, null);

        Assert.True(succeeded);
        var instance = await context.WorkflowInstances.Include(item => item.Decisions).Include(item => item.ActionLogs).SingleAsync();
        Assert.Equal(WorkflowStatus.Approved, instance.Status);
        Assert.Contains(instance.Decisions, item => item.DecisionType == WorkflowDecisionType.Approve);
        Assert.Contains(instance.ActionLogs, item => item.ActionType == WorkflowDecisionType.Approve);
    }

    [Fact]
    public async Task WorkflowService_Reject_ClosesWorkflow()
    {
        using var context = CreateContext(nameof(WorkflowService_Reject_ClosesWorkflow));
        await SeedOpenWorkflowAsync(context, "Letter", 802, "sender-1", "approver-1");
        var service = new WorkflowService(context);

        var succeeded = await service.ExecuteDecisionAsync("Letter", 802, 1, "approver-1", WorkflowDecisionType.Reject, "no", null, null, null);

        Assert.True(succeeded);
        var instance = await context.WorkflowInstances.Include(item => item.Steps).SingleAsync();
        Assert.Equal(WorkflowStatus.Rejected, instance.Status);
        Assert.NotNull(instance.ClosedAt);
        Assert.Equal(WorkflowStatus.Rejected, instance.Steps[0].Status);
    }

    [Fact]
    public async Task WorkflowService_Return_CreatesPendingPreviousStep()
    {
        using var context = CreateContext(nameof(WorkflowService_Return_CreatesPendingPreviousStep));
        await SeedTwoStepWorkflowAsync(context);
        var service = new WorkflowService(context);

        var succeeded = await service.ExecuteDecisionAsync("Letter", 803, 2, "approver-2", WorkflowDecisionType.Return, "back", null, null, null);

        Assert.True(succeeded);
        var instance = await context.WorkflowInstances.Include(item => item.Steps).SingleAsync();
        Assert.Equal(WorkflowStatus.Returned, instance.Status);
        Assert.Contains(instance.Steps, item => item.CompletedAt == null && item.ReturnedFromStepNumber == 2 && item.AssignedToUserId == "approver-1");
    }

    [Fact]
    public async Task WorkflowService_Delegate_ReassignsCurrentStep()
    {
        using var context = CreateContext(nameof(WorkflowService_Delegate_ReassignsCurrentStep));
        await SeedOpenWorkflowAsync(context, "Letter", 804, "sender-1", "approver-1");
        context.Users.Add(new User { Id = "delegate-1", UserName = "delegate", FullName = "Delegate" });
        await context.SaveChangesAsync();
        var service = new WorkflowService(context);
        var stepId = await context.WorkflowSteps.Select(item => item.Id).SingleAsync();

        var succeeded = await service.DelegateStepAsync(stepId, "approver-1", "delegate-1", "please handle");

        Assert.True(succeeded);
        var step = await context.WorkflowSteps.Include(item => item.WorkflowInstance).SingleAsync();
        Assert.Equal("delegate-1", step.AssignedToUserId);
        Assert.Equal("approver-1", step.DelegatedFromUserId);
        Assert.Equal("delegate-1", step.WorkflowInstance!.CurrentAssigneeUserId);
        Assert.Single(await context.WorkflowDelegations.ToListAsync());
    }

    [Fact]
    public async Task WorkflowService_Forward_ReassignsCurrentStep()
    {
        using var context = CreateContext(nameof(WorkflowService_Forward_ReassignsCurrentStep));
        await SeedOpenWorkflowAsync(context, "Letter", 805, "sender-1", "approver-1");
        context.Users.Add(new User { Id = "forward-1", UserName = "forward", FullName = "Forward User" });
        await context.SaveChangesAsync();
        var service = new WorkflowService(context);

        var succeeded = await service.ExecuteDecisionAsync("Letter", 805, 1, "approver-1", WorkflowDecisionType.Forward, "forward", null, null, "forward-1");

        Assert.True(succeeded);
        var step = await context.WorkflowSteps.Include(item => item.WorkflowInstance).SingleAsync();
        Assert.Equal("forward-1", step.AssignedToUserId);
        Assert.Null(step.CompletedAt);
        Assert.Equal(WorkflowStatus.PendingApproval, step.WorkflowInstance!.Status);
    }

    [Fact]
    public async Task WorkflowService_RequestChanges_ReturnsToStarter()
    {
        using var context = CreateContext(nameof(WorkflowService_RequestChanges_ReturnsToStarter));
        await SeedOpenWorkflowAsync(context, "Letter", 806, "sender-1", "approver-1");
        var service = new WorkflowService(context);

        var succeeded = await service.ExecuteDecisionAsync("Letter", 806, 1, "approver-1", WorkflowDecisionType.RequestChanges, "revise", null, null, null);

        Assert.True(succeeded);
        var instance = await context.WorkflowInstances.SingleAsync();
        Assert.Equal(WorkflowStatus.NeedsRevision, instance.Status);
        Assert.Equal("sender-1", instance.CurrentAssigneeUserId);
    }

    [Fact]
    public async Task WorkflowSlaScheduler_SchedulesCancelableJobForOpenStep()
    {
        using var context = CreateContext(nameof(WorkflowSlaScheduler_SchedulesCancelableJobForOpenStep));
        await SeedOpenWorkflowAsync(context, "Letter", 807, "sender-1", "approver-1");
        var instance = await context.WorkflowInstances.Include(item => item.Steps).SingleAsync();
        var step = Assert.Single(instance.Steps);
        var scheduler = new WorkflowSlaScheduler(context);

        var dueAt = await scheduler.ScheduleStepAsync(instance, step, 8);
        await context.SaveChangesAsync();

        Assert.NotNull(dueAt);
        Assert.Equal(dueAt, step.DueAt);
        Assert.Single(await context.WorkflowSlaJobs.Where(item => item.Status == WorkflowSlaJobStatus.Scheduled).ToListAsync());

        await scheduler.CancelStepJobsAsync(step.Id, "closed");
        await context.SaveChangesAsync();

        var job = await context.WorkflowSlaJobs.SingleAsync();
        Assert.Equal(WorkflowSlaJobStatus.Canceled, job.Status);
        Assert.NotNull(job.CanceledAt);
    }

    [Fact]
    public async Task WorkflowSlaEscalationNotifier_BreachesStepAndCreatesEscalationEvent()
    {
        var databaseName = nameof(WorkflowSlaEscalationNotifier_BreachesStepAndCreatesEscalationEvent);
        using var context = CreateContext(databaseName);
        using var officeContext = CreateOfficeContext(databaseName);
        await SeedOpenWorkflowAsync(context, "Letter", 808, "sender-1", "approver-1");
        var instance = await context.WorkflowInstances.Include(item => item.Steps).SingleAsync();
        var step = Assert.Single(instance.Steps);
        context.WorkflowSlaJobs.Add(new WorkflowSlaJob
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step.Id,
            ScheduledFor = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await context.SaveChangesAsync();

        var notifier = new WorkflowSlaEscalationNotifier(context, new NotificationService(officeContext));
        var escalated = await notifier.EscalateStepAsync(step.Id);

        Assert.True(escalated);
        Assert.Equal(WorkflowSlaState.Breached, step.SlaState);
        Assert.Single(await context.WorkflowEscalationEvents.ToListAsync());
        Assert.Single(await officeContext.Notifications.ToListAsync());
    }

    [Fact]
    public async Task WorkflowService_CreateSubCase_PausesParentUntilCompletion()
    {
        using var context = CreateContext(nameof(WorkflowService_CreateSubCase_PausesParentUntilCompletion));
        await SeedOpenWorkflowAsync(context, "Letter", 900, "sender-1", "approver-1");
        context.Users.Add(new User { Id = "helper-1", UserName = "helper", FullName = "Helper" });
        await context.SaveChangesAsync();
        var service = new WorkflowService(context);
        var parent = await context.WorkflowInstances.Include(item => item.Steps).SingleAsync(item => item.DocumentId == 900);
        var step = Assert.Single(parent.Steps);

        var subCase = await service.CreateSubCaseAsync("Letter", 900, step.Id, "approver-1", "Invoice", 901, "helper-1", "Need finance review");

        Assert.NotNull(subCase);
        Assert.Equal(WorkflowStatus.Paused, parent.Status);
        var caseTask = await context.WorkflowCaseTasks.SingleAsync(item => item.WorkflowInstanceId == parent.Id);
        Assert.Equal(WorkflowCaseTaskType.SubCase, caseTask.TaskType);

        var completed = await service.CompleteCaseTaskAsync(caseTask.Id, "helper-1", "done");

        Assert.True(completed);
        Assert.Equal(WorkflowStatus.PendingApproval, parent.Status);
    }

    [Fact]
    public async Task WorkflowService_CreateAdHocTask_AddsCaseTask()
    {
        using var context = CreateContext(nameof(WorkflowService_CreateAdHocTask_AddsCaseTask));
        await SeedOpenWorkflowAsync(context, "Letter", 902, "sender-1", "approver-1");
        context.Users.Add(new User { Id = "specialist-1", UserName = "specialist", FullName = "Specialist" });
        await context.SaveChangesAsync();
        var service = new WorkflowService(context);
        var instance = await context.WorkflowInstances.Include(item => item.Steps).SingleAsync(item => item.DocumentId == 902);
        var step = Assert.Single(instance.Steps);

        var task = await service.CreateAdHocTaskAsync("Letter", 902, step.Id, "approver-1", "specialist-1", "Check attachment", "Out-of-band review");

        Assert.NotNull(task);
        Assert.Equal(WorkflowCaseTaskType.AdHoc, task!.TaskType);
        Assert.Equal("specialist-1", task.AssignedToUserId);
        Assert.Single(await context.WorkflowCaseTasks.ToListAsync());
    }

    [Fact]
    public async Task WorkflowService_RecordsTransitionEvents_ForMining()
    {
        using var context = CreateContext(nameof(WorkflowService_RecordsTransitionEvents_ForMining));
        await SeedOpenWorkflowAsync(context, "Letter", 903, "sender-1", "approver-1");
        var service = new WorkflowService(context);

        await service.AddCommentAsync("Letter", 903, "approver-1", "reviewed");
        await service.ExecuteDecisionAsync("Letter", 903, 1, "approver-1", WorkflowDecisionType.Reject, "stop", null, null, null);

        var events = await context.WorkflowTransitionEvents.OrderBy(item => item.SequenceNumber).ToListAsync();
        Assert.NotEmpty(events);
        Assert.Contains(events, item => item.EventName == "comment.added");
        Assert.Contains(events, item => item.EventName == "workflow.reject");
    }

    [Fact]
    public async Task ConnectorExecutionService_WritesDeadLetterOnRepeatedFailure()
    {
        using var context = CreateContext(nameof(ConnectorExecutionService_WritesDeadLetterOnRepeatedFailure));
        var service = new ConnectorExecutionService(context, Options.Create(new ConnectorOptions
        {
            RetryCount = 2,
            RetryDelaySeconds = 1,
            CircuitBreakerFailures = 2,
            CircuitBreakerBreakSeconds = 5
        }));

        var result = await service.ExecuteAsync(
            new FailingConnector(),
            new ConnectorRequest { OperationName = "SendEmail", PayloadJson = "{\"to\":\"a@b.com\"}", CorrelationId = "corr-1" });

        Assert.False(result.Succeeded);
        Assert.Single(await context.ConnectorDeadLetterMessages.ToListAsync());
        Assert.Single(await context.ConnectorExecutionLogs.Where(item => !item.Succeeded).ToListAsync());
    }

    [Fact]
    public async Task ProcessMiningService_ComputesAveragesAndLoops()
    {
        using var context = CreateContext(nameof(ProcessMiningService_ComputesAveragesAndLoops));
        await SeedOpenWorkflowAsync(context, "Letter", 904, "sender-1", "approver-1");
        var instance = await context.WorkflowInstances.Include(item => item.Steps).SingleAsync(item => item.DocumentId == 904);
        var step = Assert.Single(instance.Steps);
        context.WorkflowTransitionEvents.AddRange(
            new WorkflowTransitionEvent { WorkflowInstanceId = instance.Id, WorkflowStepId = step.Id, SequenceNumber = 1, EventName = "entered", StationKey = "A", StationName = "Review A", OccurredAt = DateTimeOffset.UtcNow.AddHours(-5) },
            new WorkflowTransitionEvent { WorkflowInstanceId = instance.Id, WorkflowStepId = step.Id, SequenceNumber = 2, EventName = "entered", StationKey = "B", StationName = "Review B", OccurredAt = DateTimeOffset.UtcNow.AddHours(-3) },
            new WorkflowTransitionEvent { WorkflowInstanceId = instance.Id, WorkflowStepId = step.Id, SequenceNumber = 3, EventName = "entered", StationKey = "A", StationName = "Review A", OccurredAt = DateTimeOffset.UtcNow.AddHours(-1) });
        await context.SaveChangesAsync();

        var mining = new ProcessMiningService(context);
        var averages = await mining.GetAverageTimeInStateAsync("Letter");
        var loops = await mining.DetectReworkLoopsAsync("Letter");

        Assert.NotEmpty(averages);
        Assert.Contains(loops, item => item.StationKey == "A" && item.LoopCount > 0);
    }

    [Fact]
    public async Task TenantResolvers_PrefixCacheQueueAndStorageNames()
    {
        var databaseName = nameof(TenantResolvers_PrefixCacheQueueAndStorageNames);
        var services = new ServiceCollection()
            .AddMemoryCache()
            .Configure<TenantOptions>(options => options.DefaultTenantId = "alpha")
            .AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            context.TenantDefinitions.Add(
                new TenantDefinition
                {
                    TenantId = "alpha",
                    Name = "Alpha",
                    ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=AlphaDb;Trusted_Connection=True;",
                    LifecycleState = TenantLifecycleState.Active,
                    QueueNamespace = "alpha-q",
                    CachePrefix = "alpha-cache",
                    StorageRoot = "tenants/alpha"
                });
            await context.SaveChangesAsync();
        }

        var registry = new TenantRegistry(
            services.GetRequiredService<IOptions<TenantOptions>>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IMemoryCache>());
        var accessor = new CurrentTenantAccessor();
        accessor.Initialize(await registry.GetDefaultTenantAsync());

        var isolation = new TenantIsolationService(accessor, registry);
        var cache = new TenantCacheKeyProvider(isolation);
        var queue = new TenantQueueNameResolver(isolation);
        var path = new TenantPathResolver(isolation);

        Assert.Equal("alpha-cache:rbac:profile:user-1", cache.Prefix("rbac:profile:user-1"));
        Assert.Equal("alpha-q.officeautomation.workflow", queue.ResolveExchangeName("officeautomation.workflow"));
        Assert.Equal("/tenants/alpha/uploads/archive/file.pdf", path.GetTenantRelativePath("uploads", "archive", "file.pdf"));
    }

    [Fact]
    public async Task WorkInboxService_FiltersSlaAndUnreadItems()
    {
        using var context = CreateContext(nameof(WorkInboxService_FiltersSlaAndUnreadItems));
        await SeedOpenWorkflowAsync(context, "Letter", 809, "sender-1", "approver-1", DateTimeOffset.UtcNow.AddHours(2), WorkflowSlaState.DueSoon);
        await SeedOpenWorkflowAsync(context, "Invoice", 810, "sender-1", "approver-1", DateTimeOffset.UtcNow.AddHours(-2), WorkflowSlaState.Overdue);
        context.Notifications.Add(new Notification { RecipientUserId = "approver-1", Title = "Alert", Message = "Unread alert", Severity = NotificationSeverity.Warning });
        await context.SaveChangesAsync();
        using var identityContext = CreateIdentityContext(nameof(WorkInboxService_FiltersSlaAndUnreadItems));
        var service = new WorkInboxService(context, identityContext);

        var dueSoon = await service.BuildAsync("approver-1", false, "DueSoon");
        var overdue = await service.BuildAsync("approver-1", false, "Overdue");
        var unread = await service.BuildAsync("approver-1", false, "Unread");

        Assert.Single(dueSoon.Items, item => item.SlaState == WorkflowSlaState.DueSoon);
        Assert.Single(overdue.Items, item => item.IsOverdue);
        Assert.True(unread.Items.Count >= 3);
    }

    [Fact]
    public async Task SecurityFieldMaskingService_MasksPiiAndSensitiveAmounts()
    {
        var accessor = new FixedCurrentUserContextAccessor(new PermissionAccessProfile
        {
            UserId = "user-1",
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "HR.View",
                "Finance.View"
            }
        });

        var service = new SecurityFieldMaskingService(accessor);
        var details = new HumanCapitalDetailsVM
        {
            NationalCode = "1234567890",
            PhoneNumber = "09123456789",
            Email = "person@example.com",
            Address = "secret",
            CurrentSalary = 1000,
            SalaryHistories = [new HumanCapitalSalaryHistoryVM { PreviousSalary = 100, NewSalary = 200 }]
        };

        await service.MaskHumanCapitalDetailsAsync(details);

        Assert.Equal("******7890", details.NationalCode);
        Assert.Equal("091****89", details.PhoneNumber);
        Assert.Equal("pe***@example.com", details.Email);
        Assert.Equal("******", details.Address);
        Assert.Equal(0, details.CurrentSalary);
        Assert.Equal(0, details.SalaryHistories[0].NewSalary);
    }

    [Fact]
    public async Task SegregationOfDutiesService_BlocksSelfApprovalForInvoiceCreator()
    {
        var databaseName = nameof(SegregationOfDutiesService_BlocksSelfApprovalForInvoiceCreator);
        using var context = CreateFinanceContext(databaseName);
        using var identityContext = CreateIdentityContext(databaseName);
        using var workflowContext = CreateContext(databaseName);
        var userStore = new UserStore<User, ApplicationRole, ModularIdentityDbContext, string>(identityContext);
        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [new UserValidator<User>()],
            [new PasswordValidator<User>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<User>>.Instance);

        var invoice = new Invoice
        {
            InvoiceNumber = "INV-1",
            CreatedByUserId = "creator-1",
            PartyName = "Alpha",
            VendorName = "Alpha",
            Amount = 10,
            GrandTotal = 10,
            InvoiceDate = DateTime.Today
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var service = new SegregationOfDutiesService(userManager, identityContext, workflowContext);
        var result = await service.ValidateFinanceApprovalAsync(invoice, "creator-1");

        Assert.False(result.Allowed);
        Assert.Contains("Creator", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuditLogger_ProducesStructuredPayloadForSiem()
    {
        var scope = new CurrentDataAccessScope();
        scope.SetTenant("tenant-a");
        var logger = new AuditLogger(scope);
        var entity = new Invoice
        {
            Id = 99,
            InvoiceNumber = "INV-99",
            PartyName = "Alpha",
            VendorName = "Alpha",
            Amount = 1,
            GrandTotal = 1,
            InvoiceDate = DateTime.Today
        };
        using var context = CreateFinanceContext(nameof(AuditLogger_ProducesStructuredPayloadForSiem));
        var entry = context.Entry(entity);
        entry.State = EntityState.Added;
        var pending = new PendingAuditLogEntry(entry, new AuditRequestInfo("user-1", "user", "User One", ["FinanceApprover"], ["AuditLogs.Read"], "Privileged", "127.0.0.1", "agent", "corr-1"))
        {
            Action = "Create"
        };
        pending.NewValues["InvoiceNumber"] = "INV-99";
        pending.AffectedColumns.Add("InvoiceNumber");

        var audit = logger.Create(pending);

        Assert.NotNull(audit.StructuredPayload);
        Assert.Contains("\"eventType\":\"audit.trail\"", audit.StructuredPayload!, StringComparison.Ordinal);
        Assert.Equal("Financial", audit.ComplianceCategory);
    }

    [Fact]
    public async Task DigitalSignatureService_SignsCanonicalPayloadAndRejectsTampering()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=OfficeAutomation Test Signer",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddYears(1));
        var service = new DigitalSignatureService();
        var payload = new
        {
            documentType = "Letter",
            documentId = 42,
            values = new Dictionary<string, object?>
            {
                ["z"] = 3,
                ["a"] = "امضای معتبر"
            }
        };

        var signature = await service.SignPayloadAsync(payload, certificate);
        var equivalentPayload = new
        {
            values = new Dictionary<string, object?>
            {
                ["a"] = "امضای معتبر",
                ["z"] = 3
            },
            documentId = 42,
            documentType = "Letter"
        };
        var tamperedPayload = new
        {
            documentType = "Letter",
            documentId = 43,
            values = new Dictionary<string, object?>
            {
                ["a"] = "امضای معتبر",
                ["z"] = 3
            }
        };

        Assert.Equal(signature.CanonicalPayload, service.CanonicalizePayload(equivalentPayload));
        Assert.True(await service.VerifySignatureAsync(equivalentPayload, signature.Signature, certificate));
        Assert.False(await service.VerifySignatureAsync(tamperedPayload, signature.Signature, certificate));
        Assert.Equal("SHA256", signature.HashAlgorithm);
        Assert.False(string.IsNullOrWhiteSpace(signature.PayloadHash));
        Assert.False(string.IsNullOrWhiteSpace(signature.CertificateThumbprint));
    }

    [Fact]
    public void DocumentArchive_LegalHoldBlocksMutation()
    {
        var item = new DocumentArchiveItem
        {
            Title = "قرارداد",
            FileName = "contract.pdf",
            StoredFileName = "contract.pdf",
            RelativePath = "/tenants/default/uploads/archive/contract.pdf",
            CreatedByUserId = "user-1",
            IsUnderLegalHold = true,
            HoldReason = "پرونده قضایی فعال"
        };

        var error = Assert.Throws<InvalidOperationException>(() => DocumentArchiveController.EnsureNotUnderLegalHold(item));

        Assert.Contains("فرآیندهای قانونی", error.Message, StringComparison.Ordinal);
        Assert.Contains("پرونده قضایی فعال", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowService_TimelineLogCreation_ReturnsOrderedLogs()
    {
        using var context = CreateContext(nameof(WorkflowService_TimelineLogCreation_ReturnsOrderedLogs));
        await SeedOpenWorkflowAsync(context, "Letter", 811, "sender-1", "approver-1");
        var service = new WorkflowService(context);

        await service.AddCommentAsync("Letter", 811, "approver-1", "Looks good");
        await service.ExecuteDecisionAsync("Letter", 811, 1, "approver-1", WorkflowDecisionType.Approve, "approved", null, null, null);

        var timeline = await service.GetTimelineAsync("Letter", 811);
        Assert.True(timeline.Count >= 2);
        Assert.Contains(timeline, item => item.ActionType == WorkflowDecisionType.Comment);
        Assert.Contains(timeline, item => item.ActionType == WorkflowDecisionType.Approve);
        Assert.True(timeline.SequenceEqual(timeline.OrderByDescending(item => item.OccurredAt)));
    }

    [Fact]
    public async Task WorkflowSeedData_CreatesPresentationReadyDataset_AndIsIdempotent()
    {
        var databaseName = nameof(WorkflowSeedData_CreatesPresentationReadyDataset_AndIsIdempotent);
        using var context = CreateContext(databaseName);
        using var identityContext = CreateIdentityContext(databaseName);
        identityContext.Departments.AddRange(
            new Department { Id = 1, Name = "Financial" },
            new Department { Id = 2, Name = "Administrative" });
        await identityContext.SaveChangesAsync();

        var userStore = new UserStore<User, ApplicationRole, ModularIdentityDbContext, string>(identityContext);
        var roleStore = new RoleStore<ApplicationRole, ModularIdentityDbContext, string>(identityContext);
        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [new UserValidator<User>()],
            [new PasswordValidator<User>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<User>>.Instance);
        var roleManager = new RoleManager<ApplicationRole>(
            roleStore,
            [new RoleValidator<ApplicationRole>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);

        await WorkflowSeedData.SeedAsync(context, userManager, roleManager);
        await WorkflowSeedData.SeedAsync(context, userManager, roleManager);

        var instances = await context.WorkflowInstances.Include(item => item.Steps).ToListAsync();

        Assert.Equal(7, instances.Count(item => item.DocumentType == "SeedLetter"));
        Assert.True(await identityContext.Users.CountAsync(item => item.Id.StartsWith("seed-workflow-")) >= 5);
        Assert.True(await context.WorkflowComments.CountAsync() >= 5);
        Assert.True(await context.WorkflowAttachments.CountAsync() >= 3);
        Assert.True(await context.WorkflowDecisions.CountAsync() >= 5);

        Assert.Contains(instances, item => item.SlaState == WorkflowSlaState.Overdue);
        Assert.Contains(instances, item => item.SlaState == WorkflowSlaState.DueSoon);
        Assert.Contains(instances, item => item.Status == WorkflowStatus.Returned && item.Steps.Count > 1);
        Assert.Contains(instances, item => item.CurrentAssigneeUserId == "seed-workflow-delegate");
    }

    [Fact]
    public void IdentitySecurity_UsesStrongPasswordAndLockoutPolicy()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Program.cs")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory!.FullName, "Program.cs"));

        Assert.Contains("options.Password.RequiredLength = 8", source);
        Assert.Contains("options.Password.RequireNonAlphanumeric = true", source);
        Assert.Contains("options.Lockout.MaxFailedAccessAttempts = 5", source);
    }

    private static async Task SeedOpenWorkflowAsync(
        WorkflowDbContext context,
        string documentType,
        int documentId,
        string startedByUserId,
        string assignedToUserId,
        DateTimeOffset? dueAt = null,
        string? slaState = null)
    {
        if (!await context.Users.AnyAsync(item => item.Id == startedByUserId))
        {
            context.Users.Add(new User { Id = startedByUserId, UserName = startedByUserId, FullName = startedByUserId });
        }

        if (!await context.Users.AnyAsync(item => item.Id == assignedToUserId))
        {
            context.Users.Add(new User { Id = assignedToUserId, UserName = assignedToUserId, FullName = assignedToUserId });
        }

        var deadline = dueAt ?? DateTimeOffset.UtcNow.AddDays(1);
        context.WorkflowInstances.Add(new WorkflowInstance
        {
            DocumentType = documentType,
            DocumentId = documentId,
            Status = WorkflowStatus.PendingApproval,
            CurrentStatus = WorkflowStatus.PendingApproval,
            CurrentStepNumber = 1,
            StartedByUserId = startedByUserId,
            CurrentAssigneeUserId = assignedToUserId,
            DueAt = deadline,
            SlaState = slaState,
            Steps =
            [
                new WorkflowStep
                {
                    StepNumber = 1,
                    StepName = "Approval",
                    AssignmentMode = WorkflowAssignmentMode.User,
                    AssignedToUserId = assignedToUserId,
                    Status = WorkflowStatus.PendingApproval,
                    DueAt = deadline,
                    SlaState = slaState
                }
            ]
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedTwoStepWorkflowAsync(WorkflowDbContext context)
    {
        context.Users.AddRange(
            new User { Id = "sender-1", UserName = "sender", FullName = "Sender" },
            new User { Id = "approver-1", UserName = "approver-1", FullName = "Approver One" },
            new User { Id = "approver-2", UserName = "approver-2", FullName = "Approver Two" });

        context.WorkflowInstances.Add(new WorkflowInstance
        {
            DocumentType = "Letter",
            DocumentId = 803,
            Status = WorkflowStatus.PendingApproval,
            CurrentStatus = WorkflowStatus.PendingApproval,
            CurrentStepNumber = 2,
            StartedByUserId = "sender-1",
            CurrentAssigneeUserId = "approver-2",
            Steps =
            [
                new WorkflowStep
                {
                    StepNumber = 1,
                    StepName = "First approval",
                    AssignedToUserId = "approver-1",
                    Status = WorkflowStatus.Approved,
                    CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
                },
                new WorkflowStep
                {
                    StepNumber = 2,
                    StepName = "Second approval",
                    AssignedToUserId = "approver-2",
                    Status = WorkflowStatus.PendingApproval
                }
            ]
        });

        await context.SaveChangesAsync();
    }

    private sealed class FixedCurrentUserContextAccessor : ICurrentUserContextAccessor
    {
        private readonly PermissionAccessProfile? _profile;

        public FixedCurrentUserContextAccessor(PermissionAccessProfile? profile)
        {
            _profile = profile;
        }

        public string? UserId => _profile?.UserId;
        public PermissionAccessProfile? CurrentProfile => _profile;

        public void SetCurrentProfile(PermissionAccessProfile? profile)
        {
        }

        public Task<PermissionAccessProfile?> GetAccessProfileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_profile);
        }
    }

    [Fact]
    public void ApplySavedView_AppliesNestedAndOrFilters()
    {
        var vouchers = new[]
        {
            new SavedViewVoucherRow("V-001", "Sales invoice", "Posted", 12_500_000m),
            new SavedViewVoucherRow("V-002", "Payroll accrual", "Draft", 80_000_000m),
            new SavedViewVoucherRow("V-003", "Bank fee", "Posted", 750_000m),
            new SavedViewVoucherRow("V-004", "Sales adjustment", "Posted", 2_000_000m)
        }.AsQueryable();

        const string filterJson = """
        {
          "logic": "and",
          "filters": [
            { "field": "Status", "operator": "eq", "value": "Posted" },
            {
              "logic": "or",
              "filters": [
                { "field": "Description", "operator": "contains", "value": "Sales" },
                { "field": "TotalAmount", "operator": "lt", "value": 1000000 }
              ]
            }
          ]
        }
        """;

        var result = vouchers
            .ApplySavedView(filterJson, ["Status", "Description", "TotalAmount"])
            .Select(item => item.VoucherNumber)
            .OrderBy(item => item)
            .ToList();

        Assert.Equal(["V-001", "V-003", "V-004"], result);
    }

    [Fact]
    public void ApplySavedView_FiltersNestedFloatingDetailFields()
    {
        var vouchers = new[]
        {
            new SavedViewVoucherRow("V-010", "Receipt", "Posted", 10_000m, new SavedViewFloatingDetail("CUST-01", "Alpha Project")),
            new SavedViewVoucherRow("V-011", "Receipt", "Posted", 12_000m, new SavedViewFloatingDetail("COST-02", "Cost Center Two")),
            new SavedViewVoucherRow("V-012", "Receipt", "Draft", 14_000m, new SavedViewFloatingDetail("CUST-03", "Beta Project"))
        }.AsQueryable();

        const string filterJson = """
        {
          "logic": "and",
          "filters": [
            { "field": "Status", "operator": "eq", "value": "Posted" },
            { "field": "FloatingDetailAccount.Name", "operator": "contains", "value": "Project" }
          ]
        }
        """;

        var result = vouchers
            .ApplySavedView(filterJson, ["Status", "FloatingDetailAccount.Name", "FloatingDetailAccount.Code"])
            .Select(item => item.VoucherNumber)
            .ToList();

        Assert.Equal(["V-010"], result);
    }

    [Fact]
    public void TableSchemaRegistry_MasksColumnsDeniedByRole()
    {
        var registry = new TableSchemaRegistry();
        const string layoutJson = """
        [
          { "columnId": "voucherNumber", "order": 1, "width": 120, "isVisible": true },
          { "columnId": "totalAmount", "order": 2, "width": 160, "isVisible": true },
          { "columnId": "description", "order": 3, "width": 240, "isVisible": true }
        ]
        """;

        var maskedJson = registry.MaskColumnLayoutJson("Finance_Vouchers", layoutJson, ["Finance.Clerk"]);

        Assert.Contains("voucherNumber", maskedJson);
        Assert.Contains("description", maskedJson);
        Assert.DoesNotContain("totalAmount", maskedJson);
    }

    private sealed record SavedViewVoucherRow(
        string VoucherNumber,
        string Description,
        string Status,
        decimal TotalAmount,
        SavedViewFloatingDetail? FloatingDetailAccount = null);

    private sealed record SavedViewFloatingDetail(
        string Code,
        string Name);

    private static ICurrentDataAccessScope CreateScope(PermissionAccessProfile? profile)
    {
        var scope = new CurrentDataAccessScope();
        scope.Initialize(profile?.UserId, profile);
        return scope;
    }

    private sealed class FailingConnector : IExternalConnector
    {
        public string Name => "FailingConnector";

        public Task<ConnectorResponse> ExecuteAsync(ConnectorRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("connector failure");
        }
    }
}




