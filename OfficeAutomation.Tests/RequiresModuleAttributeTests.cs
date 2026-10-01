using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OfficeAutomation.Controllers;
using OfficeAutomation.Controllers.Api;
using OfficeAutomation.Controllers.Api.v1;
using OfficeAutomation.Controllers.Financial;
using OfficeAutomation.Services.Licensing;
using Xunit;

namespace OfficeAutomation.Tests;

public class RequiresModuleAttributeTests
{
    private static ActionExecutingContext CreateActionExecutingContext(
        ILicenseContext licenseContext,
        string path = "/TestModule/Index",
        bool isApiOrJson = false,
        string? queryString = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(licenseContext);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        httpContext.Request.Path = path;

        if (isApiOrJson)
        {
            httpContext.Request.Headers["Accept"] = "application/json";
        }

        if (!string.IsNullOrEmpty(queryString))
        {
            httpContext.Request.QueryString = new QueryString(queryString);
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    [Fact]
    public async Task WhenModuleIsLicensed_AllowsExecution()
    {
        // Arrange: License has "Contracts"
        var mockContext = new Mock<ILicenseContext>();
        mockContext.Setup(c => c.IsModuleEnabled("Contracts")).Returns(true);

        var filter = new RequiresModuleAttribute("Contracts");
        var context = CreateActionExecutingContext(mockContext.Object);
        var executed = false;
        ActionExecutionDelegate next = () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        };

        // Act
        await filter.OnActionExecutionAsync(context, next);

        // Assert
        Assert.True(executed);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task WhenModuleIsNotLicensed_BrowserRequest_RedirectsToModuleNotLicensedPage()
    {
        // Arrange: License only has "Contracts", user visits "Financial"
        var mockContext = new Mock<ILicenseContext>();
        mockContext.Setup(c => c.IsModuleEnabled("Financial")).Returns(false);

        var filter = new RequiresModuleAttribute("Financial");
        var context = CreateActionExecutingContext(mockContext.Object, path: "/Financial/Dashboard");
        var executed = false;
        ActionExecutionDelegate next = () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        };

        // Act
        await filter.OnActionExecutionAsync(context, next);

        // Assert
        Assert.False(executed);
        Assert.NotNull(context.Result);
        var redirectResult = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("ModuleNotLicensed", redirectResult.ActionName);
        Assert.Equal("Home", redirectResult.ControllerName);
        Assert.NotNull(redirectResult.RouteValues);
        Assert.Equal("Financial", redirectResult.RouteValues["module"]);
    }

    [Fact]
    public async Task WhenModuleIsNotLicensed_WithTabQuery_PreservesTabParameterInRedirect()
    {
        // Arrange: In an iframe tab
        var mockContext = new Mock<ILicenseContext>();
        mockContext.Setup(c => c.IsModuleEnabled("Financial")).Returns(false);

        var filter = new RequiresModuleAttribute("Financial");
        var context = CreateActionExecutingContext(mockContext.Object, path: "/Financial/Ledger", queryString: "?_tab=1");
        ActionExecutionDelegate next = () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));

        // Act
        await filter.OnActionExecutionAsync(context, next);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.NotNull(redirectResult.RouteValues);
        Assert.Equal("1", redirectResult.RouteValues["_tab"]);
        Assert.Equal("Financial", redirectResult.RouteValues["module"]);
    }

    [Fact]
    public async Task WhenModuleIsNotLicensed_ApiOrJsonRequest_Returns403JsonResult()
    {
        // Arrange: API call to unlicensed module
        var mockContext = new Mock<ILicenseContext>();
        mockContext.Setup(c => c.IsModuleEnabled("Financial")).Returns(false);

        var filter = new RequiresModuleAttribute("Financial");
        var context = CreateActionExecutingContext(mockContext.Object, path: "/api/v1/finance/vouchers", isApiOrJson: true);
        var executed = false;
        ActionExecutionDelegate next = () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        };

        // Act
        await filter.OnActionExecutionAsync(context, next);

        // Assert
        Assert.False(executed);
        Assert.NotNull(context.Result);
        var jsonResult = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    [Fact]
    public async Task WithContractsOnlyLicense_ContractsSucceeds_AndFinancialRedirects()
    {
        // Scenario 5 from requirements:
        // A test license that only includes ["Contracts"]
        var license = new LicenseInfo
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Solo Contracts Client",
            CustomerId = "CLI-CONTRACTS-ONLY",
            Modules = new List<string> { "Contracts" },
            MaxUsers = 10,
            LicenseType = "subscription",
            IssuedAtUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };

        var mockContext = new Mock<ILicenseContext>();
        mockContext.Setup(c => c.IsLicensed).Returns(true);
        mockContext.Setup(c => c.IsLocked).Returns(false);
        mockContext.Setup(c => c.CurrentLicense).Returns(license);
        mockContext.Setup(c => c.IsModuleEnabled(It.IsAny<string>()))
            .Returns<string>(m => license.HasModule(m));

        // Verify license context state
        Assert.True(mockContext.Object.IsModuleEnabled("Contracts"));
        Assert.False(mockContext.Object.IsModuleEnabled("Financial"));

        // 1. Direct access to ContractsController: succeeds
        var contractsFilter = new RequiresModuleAttribute("Contracts");
        var contractsContext = CreateActionExecutingContext(mockContext.Object, path: "/Contracts/Index");
        var contractsExecuted = false;
        await contractsFilter.OnActionExecutionAsync(contractsContext, () =>
        {
            contractsExecuted = true;
            return Task.FromResult(new ActionExecutedContext(contractsContext, new List<IFilterMetadata>(), new object()));
        });
        Assert.True(contractsExecuted);
        Assert.Null(contractsContext.Result);

        // 2. Direct access to FinancialController: blocked and redirected
        var financialFilter = new RequiresModuleAttribute("Financial");
        var financialContext = CreateActionExecutingContext(mockContext.Object, path: "/Financial/Dashboard");
        var financialExecuted = false;
        await financialFilter.OnActionExecutionAsync(financialContext, () =>
        {
            financialExecuted = true;
            return Task.FromResult(new ActionExecutedContext(financialContext, new List<IFilterMetadata>(), new object()));
        });
        Assert.False(financialExecuted);
        Assert.NotNull(financialContext.Result);
        var redirect = Assert.IsType<RedirectToActionResult>(financialContext.Result);
        Assert.Equal("ModuleNotLicensed", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.Equal("Financial", redirect.RouteValues?["module"]);
    }

    [Theory]
    [InlineData("Financial", "Finance", true)]
    [InlineData("Finance", "Financial", true)]
    [InlineData("Contracts", "Contract", true)]
    [InlineData("Contract", "Contracts", true)]
    [InlineData("Contracts", "Warehouse", false)]
    public void LicenseInfo_HasModule_SupportsAliases(string licensedModule, string queriedModule, bool expected)
    {
        var license = new LicenseInfo
        {
            Modules = new List<string> { licensedModule }
        };

        Assert.Equal(expected, license.HasModule(queriedModule));
    }

    [Theory]
    // Contracts
    [InlineData(typeof(ContractsController), LicensableModules.Contracts)]
    [InlineData(typeof(ContractGuaranteesController), LicensableModules.Contracts)]
    [InlineData(typeof(PaymentPenaltiesController), LicensableModules.Contracts)]
    [InlineData(typeof(DocumentRequirementsController), LicensableModules.Contracts)]
    // Financial
    [InlineData(typeof(FinancialController), LicensableModules.Financial)]
    [InlineData(typeof(AccountReviewController), LicensableModules.Financial)]
    [InlineData(typeof(VouchersController), LicensableModules.Financial)]
    [InlineData(typeof(CommercialController), LicensableModules.Financial)]
    [InlineData(typeof(InvoiceIntakeController), LicensableModules.Financial)]
    [InlineData(typeof(InvoiceDashboardController), LicensableModules.Financial)]
    [InlineData(typeof(MowadiyanController), LicensableModules.Financial)]
    [InlineData(typeof(TaxAuditController), LicensableModules.Financial)]
    [InlineData(typeof(TaxAnalyticsController), LicensableModules.Financial)]
    [InlineData(typeof(PayrollController), LicensableModules.Financial)]
    [InlineData(typeof(BimehController), LicensableModules.Financial)]
    [InlineData(typeof(FinanceApiController), LicensableModules.Financial)]
    [InlineData(typeof(ChartOfAccountsApiController), LicensableModules.Financial)]
    // Warehouse
    [InlineData(typeof(WarehouseController), LicensableModules.Warehouse)]
    [InlineData(typeof(WarehouseInventoryController), LicensableModules.Warehouse)]
    [InlineData(typeof(WarehouseIssuancesController), LicensableModules.Warehouse)]
    [InlineData(typeof(WarehouseReceiptsController), LicensableModules.Warehouse)]
    [InlineData(typeof(WarehouseTransferController), LicensableModules.Warehouse)]
    [InlineData(typeof(VendorsController), LicensableModules.Warehouse)]
    [InlineData(typeof(WaybillController), LicensableModules.Warehouse)]
    // HumanCapital
    [InlineData(typeof(HumanCapitalController), LicensableModules.HumanCapital)]
    [InlineData(typeof(LeavesController), LicensableModules.HumanCapital)]
    [InlineData(typeof(EmployersController), LicensableModules.HumanCapital)]
    [InlineData(typeof(OrganizationCalendarController), LicensableModules.HumanCapital)]
    [InlineData(typeof(HrApiController), LicensableModules.HumanCapital)]
    // OfficeAutomation
    [InlineData(typeof(LettersController), LicensableModules.OfficeAutomation)]
    [InlineData(typeof(DocumentArchiveController), LicensableModules.OfficeAutomation)]
    [InlineData(typeof(DocumentApiController), LicensableModules.OfficeAutomation)]
    // Workflow
    [InlineData(typeof(WorkInboxController), LicensableModules.Workflow)]
    [InlineData(typeof(WorkInboxApiController), LicensableModules.Workflow)]
    [InlineData(typeof(WorkflowApiController), LicensableModules.Workflow)]
    [InlineData(typeof(ProcessAnalysisController), LicensableModules.Workflow)]
    // AiAssistant
    [InlineData(typeof(AiAssistantController), LicensableModules.AiAssistant)]
    public void Controllers_HaveRequiresModuleAttribute_WithCorrectModule(Type controllerType, string expectedModule)
    {
        var attr = controllerType.GetCustomAttribute<RequiresModuleAttribute>(inherit: true);

        Assert.NotNull(attr);
        Assert.Equal(expectedModule, attr.ModuleName);
    }

    [Fact]
    public void AllControllers_MustHaveEitherRequiresModuleOrLicenseExempt()
    {
        // Act: scan the entire web assembly
        var uncovered = LicensableModules.FindUncoveredControllers(typeof(HomeController).Assembly);

        // Assert: zero unannotated controllers allowed
        Assert.Empty(uncovered);
    }

    [Theory]
    [InlineData(typeof(HomeController))]
    [InlineData(typeof(SettingsController))]
    [InlineData(typeof(SecurityController))]
    [InlineData(typeof(UsersController))]
    [InlineData(typeof(DepartmentsController))]
    [InlineData(typeof(OfficeAutomation.Controllers.Api.AuthApiController))]
    public void CoreControllers_HaveLicenseExemptAttribute(Type controllerType)
    {
        var attr = controllerType.GetCustomAttribute<LicenseExemptAttribute>(inherit: true);

        Assert.NotNull(attr);
    }
}
