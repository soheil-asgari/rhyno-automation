using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OfficeAutomation.Controllers.Api.v1;
using OfficeAutomation.DTOs.Api.v1;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Services.Tenancy;
using Xunit;

namespace OfficeAutomation.Tests;

/// <summary>
/// تست‌های واحد و یکپارچه‌سازی برای کنترلرهای API
/// </summary>
public sealed class ApiControllersTests
{
    private static WorkflowDbContext CreateWorkflowContext(string databaseName)
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

    #region FinanceApiController Tests

    [Fact]
    public async Task FinanceApi_GetVouchers_ReturnsPagedResult()
    {
        using var context = CreateFinanceContext(nameof(FinanceApi_GetVouchers_ReturnsPagedResult));
        var workflowService = new WorkflowService();
        var ledgerService = CreateMock<FinanceLedgerService>();
        var invoiceService = CreateFinanceInvoiceService(context);

        context.Invoices.Add(new Invoice
        {
            InvoiceNumber = "INV-001",
            InvoiceType = "Sale",
            DateShamsi = "1403/04/15",
            PartyName = "شرکت تست",
            Amount = 1000000,
            InvoiceDate = DateTime.Now,
            WorkflowStatus = WorkflowStatus.Sent,
            VendorName = "شرکت تست"
        });
        await context.SaveChangesAsync();

        var controller = CreateFinanceController(context, workflowService, ledgerService, invoiceService);
        var filter = new VoucherFilterRequest { Page = 1, PageSize = 20 };

        var result = await controller.GetVouchers(filter, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task FinanceApi_GetVoucher_ReturnsNotFound_WhenMissing()
    {
        using var context = CreateFinanceContext(nameof(FinanceApi_GetVoucher_ReturnsNotFound_WhenMissing));
        var workflowService = new WorkflowService();
        var ledgerService = CreateMock<FinanceLedgerService>();
        var invoiceService = CreateFinanceInvoiceService(context);

        var controller = CreateFinanceController(context, workflowService, ledgerService, invoiceService);

        var result = await controller.GetVoucher(999, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task FinanceApi_CreateVoucher_ReturnsBadRequest_WhenModelInvalid()
    {
        using var context = CreateFinanceContext(nameof(FinanceApi_CreateVoucher_ReturnsBadRequest_WhenModelInvalid));
        var workflowService = new WorkflowService();
        var ledgerService = CreateMock<FinanceLedgerService>();
        var invoiceService = CreateFinanceInvoiceService(context);

        var controller = CreateFinanceController(context, workflowService, ledgerService, invoiceService);
        controller.ModelState.AddModelError("InvoiceNumber", "شماره فاکتور الزامی است.");

        var request = new CreateVoucherRequest();
        var result = await controller.CreateVoucher(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task FinanceApi_CreateVoucher_SuccessfullyCreatesInvoice()
    {
        using var context = CreateFinanceContext(nameof(FinanceApi_CreateVoucher_SuccessfullyCreatesInvoice));
        var workflowService = new WorkflowService();
        var ledgerService = CreateMock<FinanceLedgerService>();
        var invoiceService = CreateFinanceInvoiceService(context);

        var controller = CreateFinanceController(context, workflowService, ledgerService, invoiceService);

        var request = new CreateVoucherRequest
        {
            InvoiceNumber = "INV-002",
            InvoiceType = "Sale",
            DateShamsi = "1403/05/10",
            PartyName = "شرکت نمونه",
            Amount = 5000000,
            Items = new List<CreateVoucherItemDto>
            {
                new() { ProductId = 1, ItemName = "محصول تست", Quantity = 2, UnitPrice = 2500000 }
            }
        };

        var result = await controller.CreateVoucher(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var savedInvoice = await context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == "INV-002");
        Assert.NotNull(savedInvoice);
    }

    [Fact]
    public async Task FinanceApi_GetBalanceReport_ReturnsReport()
    {
        using var context = CreateFinanceContext(nameof(FinanceApi_GetBalanceReport_ReturnsReport));
        var workflowService = new WorkflowService();
        var ledgerService = CreateMock<FinanceLedgerService>();
        var invoiceService = CreateFinanceInvoiceService(context);

        var controller = CreateFinanceController(context, workflowService, ledgerService, invoiceService);

        var result = await controller.GetBalanceReport(null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    #endregion

    #region DocumentApiController Tests

    [Fact]
    public async Task DocumentApi_GetDocuments_ReturnsPagedResult()
    {
        using var context = CreateWorkflowContext(nameof(DocumentApi_GetDocuments_ReturnsPagedResult));
        var controller = CreateDocumentController(context);

        context.DocumentArchiveItems.Add(new DocumentArchiveItem
        {
            Title = "سند تست",
            Category = DocumentArchiveModules.Letters,
            AccessLevel = DocumentArchiveVisibilityLevels.Public,
            FileName = "test.pdf",
            StoredFileName = "abc123.pdf",
            RelativePath = "uploads/archive/abc123.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            CreatedByUserId = "test-user"
        });
        await context.SaveChangesAsync();

        var filter = new DocumentFilterRequest { Page = 1, PageSize = 20 };

        var result = await controller.GetDocuments(filter, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task DocumentApi_Upload_ReturnsBadRequest_WhenNoFile()
    {
        using var context = CreateWorkflowContext(nameof(DocumentApi_Upload_ReturnsBadRequest_WhenNoFile));
        var controller = CreateDocumentController(context);

        controller.ModelState.AddModelError("File", "فایل الزامی است.");
        var request = new DocumentUploadRequest
        {
            Title = "سند جدید",
            File = null!,
            Category = "Letters",
            AccessLevel = "Public"
        };

        var result = await controller.UploadDocument(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task DocumentApi_Download_ReturnsNotFound_WhenMissing()
    {
        using var context = CreateWorkflowContext(nameof(DocumentApi_Download_ReturnsNotFound_WhenMissing));
        var controller = CreateDocumentController(context);

        var result = await controller.DownloadDocument(999, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    #endregion

    #region WorkflowApiController Tests

    [Fact]
    public async Task WorkflowApi_GetPendingTasks_ReturnsList()
    {
        using var context = CreateWorkflowContext(nameof(WorkflowApi_GetPendingTasks_ReturnsList));
        var workflowService = new WorkflowService();
        var controller = CreateWorkflowController(context, workflowService);

        var result = await controller.GetPendingTasks(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task WorkflowApi_ApproveTask_ReturnsNotFound_WhenMissing()
    {
        using var context = CreateWorkflowContext(nameof(WorkflowApi_ApproveTask_ReturnsNotFound_WhenMissing));
        var workflowService = new WorkflowService();
        var controller = CreateWorkflowController(context, workflowService);

        var result = await controller.ApproveTask(999, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task WorkflowApi_RejectTask_ReturnsBadRequest_WhenNoReason()
    {
        using var context = CreateWorkflowContext(nameof(WorkflowApi_RejectTask_ReturnsBadRequest_WhenNoReason));
        var workflowService = new WorkflowService();
        var controller = CreateWorkflowController(context, workflowService);

        var result = await controller.RejectTask(1, new TaskDecisionRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region HrApiController Tests

    [Fact]
    public async Task HrApi_GetEmployees_ReturnsPagedResult()
    {
        using var context = CreateOfficeContext(nameof(HrApi_GetEmployees_ReturnsPagedResult));
        var controller = CreateHrController(context);

        var filter = new EmployeeFilterRequest { Page = 1, PageSize = 20 };

        var result = await controller.GetEmployees(filter, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task HrApi_CreateLeave_ReturnsBadRequest_WhenDatesInvalid()
    {
        using var context = CreateOfficeContext(nameof(HrApi_CreateLeave_ReturnsBadRequest_WhenDatesInvalid));
        var controller = CreateHrController(context);

        var request = new CreateLeaveRequest
        {
            StartDate = DateTime.Now.AddDays(5),
            EndDate = DateTime.Now,
            Reason = "مرخصی استعلاجی"
        };

        var result = await controller.CreateLeave(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task HrApi_CreateLeave_SuccessfullyCreatesLeave()
    {
        using var context = CreateOfficeContext(nameof(HrApi_CreateLeave_SuccessfullyCreatesLeave));
        var controller = CreateHrController(context);

        var request = new CreateLeaveRequest
        {
            StartDate = DateTime.Now.AddDays(1),
            EndDate = DateTime.Now.AddDays(3),
            Reason = "مرخصی شخصی"
        };

        var result = await controller.CreateLeave(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var savedLeave = await context.Leaves.FirstOrDefaultAsync(l => l.Reason == "مرخصی شخصی");
        Assert.NotNull(savedLeave);
    }

    [Fact]
    public async Task HrApi_GetLeaves_ReturnsPagedResult()
    {
        using var context = CreateOfficeContext(nameof(HrApi_GetLeaves_ReturnsPagedResult));
        var controller = CreateHrController(context);

        var result = await controller.GetLeaves(null, null, 1, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    #endregion

    #region Helper Methods

    private FinanceApiController CreateFinanceController(
        FinanceDbContext context,
        WorkflowService workflowService,
        FinanceLedgerService ledgerService,
        FinancialInvoiceService invoiceService)
    {
        var controller = new FinanceApiController(context, workflowService, ledgerService, invoiceService);
        SetupControllerContext(controller);
        return controller;
    }

    private DocumentApiController CreateDocumentController(IWorkflowDbContext context)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());
        env.SetupGet(e => e.WebRootPath).Returns(Path.GetTempPath());

        var tenantResolver = new Mock<ITenantPathResolver>();
        tenantResolver.Setup(t => t.GetArchiveRoot(It.IsAny<string>())).Returns(Path.Combine(Path.GetTempPath(), "archive-test"));
        tenantResolver.Setup(t => t.GetTenantRelativePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("/", parts));
        tenantResolver.Setup(t => t.MapRelativeToPhysical(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string basePath, string relPath) => Path.Combine(basePath, relPath));

        var permService = new Mock<IPermissionAccessService>();
        permService.Setup(p => p.UserHasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new DocumentApiController(context, env.Object, tenantResolver.Object, permService.Object);
        SetupControllerContext(controller);
        return controller;
    }

    private WorkflowApiController CreateWorkflowController(IWorkflowDbContext context, WorkflowService workflowService)
    {
        var controller = new WorkflowApiController(context, workflowService);
        SetupControllerContext(controller);
        return controller;
    }

    private HrApiController CreateHrController(OfficeDbContext context)
    {
        var workflowContext = CreateWorkflowContext(Guid.NewGuid().ToString());
        var workflowService = new WorkflowService();
        var leaveWorkflowService = new LeaveWorkflowService(workflowContext, workflowService);
        var controller = new HrApiController(context, workflowService, leaveWorkflowService);
        SetupControllerContext(controller);
        return controller;
    }

    private static void SetupControllerContext(ControllerBase controller)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = "test-tenant";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim("auth_method", "apikey")
        }, "ApiKey"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static FinancialInvoiceService CreateFinanceInvoiceService(FinanceDbContext context)
    {
        var identityOptions = new DbContextOptionsBuilder<Modules.Identity.Infrastructure.Persistence.IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var identityContext = new Modules.Identity.Infrastructure.Persistence.IdentityDbContext(identityOptions);
        return new FinancialInvoiceService(context, identityContext);
    }

    private static T CreateMock<T>() where T : class
    {
        return new Mock<T>().Object;
    }

    #endregion
}