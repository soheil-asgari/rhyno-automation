using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OfficeAutomation.Controllers;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Contracts;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Services.Tenancy;
using Xunit;

namespace OfficeAutomation.Tests;

/// <summary>
/// Security-focused tests for the Contracts module: object-level authorization
/// (IDOR), status-transition enforcement, and attachment access control.
/// </summary>
public sealed class ContractSecurityTests
{
    private static OfficeDbContext CreateOfficeContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<OfficeDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OfficeDbContext(options);
    }

    private static UserManager<User> CreateUserManager()
    {
        var store = new Mock<IUserStore<User>>();
        return new UserManager<User>(
            store.Object,
            null!, null!, null!, null!, null!, null!, null!, NullLogger<UserManager<User>>.Instance);
    }

    private static ContractService CreateService(OfficeDbContext context, UserManager<User> userManager)
    {
        var auditLogger = new Mock<IAuditLogger>();
        var templateRenderer = new Mock<ITemplateRenderingService>();
        templateRenderer.Setup(t => t.RenderTemplate(It.IsAny<string>(), It.IsAny<Contract>(), It.IsAny<Customer?>(), It.IsAny<User?>()))
            .Returns((string html, Contract c, Customer? cust, User? u) => html);
        var tenantSettings = new Mock<ITenantSettingsService>();
        var versionService = new Mock<IVersionService>();
        var contractAccounting = new Mock<IContractAccountingService>();
        var penaltyService = new Mock<IPaymentPenaltyService>();
        var documentRequirementService = new Mock<IDocumentRequirementService>();
        var logger = NullLogger<ContractService>.Instance;
        return new ContractService(
            context,
            userManager,
            logger,
            auditLogger.Object,
            templateRenderer.Object,
            tenantSettings.Object,
            versionService.Object,
            contractAccounting.Object,
            penaltyService.Object,
            documentRequirementService.Object);
    }

    private static async Task<Contract> SeedContractAsync(OfficeDbContext context, string ownerUserId, decimal amount = 1000)
    {
        var contract = new Contract
        {
            ContractNumber = "CTR-1403-001",
            Title = "Test Contract",
            ClientName = "Client A",
            TotalAmount = amount,
            PaidAmount = 0,
            RemainingBalance = amount,
            PaymentStatus = PaymentStatuses.Unpaid,
            Status = ContractStatuses.Draft,
            CreatedByUserId = ownerUserId,
            AssignedUserId = ownerUserId,
            StartDate = DateTime.Today,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();
        return contract;
    }

    // ── IDOR: non-owner cannot add a payment to another user's contract ──
    [Fact]
    public async Task AddPayment_ByNonOwner_ThrowsUnauthorized()
    {
        using var context = CreateOfficeContext(nameof(AddPayment_ByNonOwner_ThrowsUnauthorized));
        var ownerId = "user-owner";
        var attackerId = "user-attacker";
        var contract = await SeedContractAsync(context, ownerId);
        var service = CreateService(context, CreateUserManager());

        var model = new ContractPaymentCreateVM
        {
            ContractId = contract.Id,
            Amount = 100,
            PaymentDate = DateTime.Today,
            Status = "Paid"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddPaymentAsync(model, attackerId, CancellationToken.None));
    }

    // ── IDOR: non-owner cannot delete a payment from another user's contract ──
    [Fact]
    public async Task DeletePayment_ByNonOwner_ThrowsUnauthorized()
    {
        using var context = CreateOfficeContext(nameof(DeletePayment_ByNonOwner_ThrowsUnauthorized));
        var ownerId = "user-owner";
        var attackerId = "user-attacker";
        var contract = await SeedContractAsync(context, ownerId);

        var payment = new ContractPayment
        {
            ContractId = contract.Id,
            Amount = 50,
            PaymentDate = DateTime.Today,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow
        };
        context.ContractPayments.Add(payment);
        await context.SaveChangesAsync();

        var service = CreateService(context, CreateUserManager());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeletePaymentAsync(payment.Id, contract.Id, attackerId, CancellationToken.None));
    }

    // ── Attachment download requires authorization on the parent contract ──
    [Fact]
    public async Task GetAttachmentForDownload_ByNonOwner_ThrowsUnauthorized()
    {
        using var context = CreateOfficeContext(nameof(GetAttachmentForDownload_ByNonOwner_ThrowsUnauthorized));
        var ownerId = "user-owner";
        var attackerId = "user-attacker";
        var contract = await SeedContractAsync(context, ownerId);

        var attachment = new ContractAttachment
        {
            ContractId = contract.Id,
            FileName = "a.pdf",
            StoredFileName = "guid_a.pdf",
            RelativePath = $"/uploads/contracts/{contract.Id}/guid_a.pdf",
            UploadedByUserId = ownerId,
            CreatedAt = DateTime.UtcNow
        };
        context.ContractAttachments.Add(attachment);
        await context.SaveChangesAsync();

        var service = CreateService(context, CreateUserManager());
        // Access to the attachment must be denied for a non-owner of the parent contract.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetAttachmentForDownloadAsync(attachment.Id, attackerId, CancellationToken.None));
    }

    // ── Status transition: Draft → Completed is invalid for a non-admin ──
    [Fact]
    public async Task UpdateContract_InvalidStatusTransition_ThrowsUnauthorized()
    {
        using var context = CreateOfficeContext(nameof(UpdateContract_InvalidStatusTransition_ThrowsUnauthorized));
        var ownerId = "user-owner";
        var contract = await SeedContractAsync(context, ownerId);
        var service = CreateService(context, CreateUserManager());

        var model = new ContractEditVM
        {
            Id = contract.Id,
            ContractNumber = contract.ContractNumber,
            Title = contract.Title,
            ClientName = contract.ClientName,
            TotalAmount = contract.TotalAmount,
            StartDate = contract.StartDate,
            Status = ContractStatuses.Completed,
            AssignedUserId = ownerId
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateContractAsync(model, ownerId, CancellationToken.None));
    }

    // ── Owner can add a payment to their own contract ──
    [Fact]
    public async Task AddPayment_ByOwner_Succeeds()
    {
        using var context = CreateOfficeContext(nameof(AddPayment_ByOwner_Succeeds));
        var ownerId = "user-owner";
        var contract = await SeedContractAsync(context, ownerId);
        var service = CreateService(context, CreateUserManager());

        var model = new ContractPaymentCreateVM
        {
            ContractId = contract.Id,
            Amount = 100,
            PaymentDate = DateTime.Today,
            Status = "Paid"
        };

        var payment = await service.AddPaymentAsync(model, ownerId, CancellationToken.None);
        Assert.NotNull(payment);
        Assert.Equal(contract.Id, payment.ContractId);
    }

    [Fact]
    public async Task CreateContract_ExclusiveTax_CalculatesCorrectly()
    {
        using var context = CreateOfficeContext(nameof(CreateContract_ExclusiveTax_CalculatesCorrectly));
        var userId = "user-creator";
        var service = CreateService(context, CreateUserManager());

        var model = new ContractCreateVM
        {
            ContractNumber = "CNT-TAX-001",
            Title = "قرارداد تست با مالیات افزوده",
            ClientName = "مشتری تست",
            TotalAmount = 1000m,
            CurrencyCode = CurrencyCode.USD,
            ExchangeRate = 500000m,
            TaxMethod = TaxCalculationMethod.Exclusive,
            DefaultTaxRatePercent = 10m,
            StartDate = DateTime.Today
        };

        var contract = await service.CreateContractAsync(model, userId, CancellationToken.None);

        Assert.Equal(100m, contract.TotalTaxAmount);
        Assert.Equal(1100m, contract.TotalAmountInclusiveTax);
        Assert.Equal(500000000m, contract.BaseCurrencyAmount);
        Assert.Equal(CurrencyCode.USD, contract.CurrencyCode);
        Assert.Equal(500000m, contract.ExchangeRate);
    }

    [Fact]
    public async Task CreateContract_InclusiveTax_CalculatesCorrectly()
    {
        using var context = CreateOfficeContext(nameof(CreateContract_InclusiveTax_CalculatesCorrectly));
        var userId = "user-creator";
        var service = CreateService(context, CreateUserManager());

        var model = new ContractCreateVM
        {
            ContractNumber = "CNT-TAX-002",
            Title = "قرارداد تست با مالیات گنجانده شده",
            ClientName = "مشتری تست",
            TotalAmount = 1100m,
            CurrencyCode = CurrencyCode.IRR,
            ExchangeRate = 1m,
            TaxMethod = TaxCalculationMethod.Inclusive,
            DefaultTaxRatePercent = 10m,
            StartDate = DateTime.Today
        };

        var contract = await service.CreateContractAsync(model, userId, CancellationToken.None);

        Assert.Equal(100m, contract.TotalTaxAmount);
        Assert.Equal(1100m, contract.TotalAmountInclusiveTax);
        Assert.Equal(1100m, contract.BaseCurrencyAmount);
    }

    [Fact]
    public async Task GenerateNextContractNumber_WithCustomContractNumbers_DoesNotResetAndIncrements()
    {
        using var context = CreateOfficeContext(nameof(GenerateNextContractNumber_WithCustomContractNumbers_DoesNotResetAndIncrements));
        var userId = "user-creator";
        var service = CreateService(context, CreateUserManager());

        var pc = new System.Globalization.PersianCalendar();
        int year = pc.GetYear(DateTime.Now);

        // Seed custom format contract numbers that would previously break string-splitting logic
        context.Contracts.Add(new Contract
        {
            ContractNumber = "AGREEMENT-2026/09",
            Title = "Custom Contract 1",
            ClientName = "Custom Client 1",
            TotalAmount = 500,
            CreatedByUserId = userId,
            AssignedUserId = userId
        });
        context.Contracts.Add(new Contract
        {
            ContractNumber = "RH-01",
            Title = "Custom Contract 2",
            ClientName = "Custom Client 2",
            TotalAmount = 500,
            CreatedByUserId = userId,
            AssignedUserId = userId
        });
        await context.SaveChangesAsync();

        var num1 = await service.GenerateNextContractNumberAsync();
        var num2 = await service.GenerateNextContractNumberAsync();

        Assert.Equal($"CTR-{year}-001", num1);
        Assert.Equal($"CTR-{year}-002", num2);
    }

    [Fact]
    public async Task CreateContractAsync_ConcurrentRequests_GeneratesUniqueSequentialContractNumbers()
    {
        var dbName = nameof(CreateContractAsync_ConcurrentRequests_GeneratesUniqueSequentialContractNumbers);
        using var context = CreateOfficeContext(dbName);
        var userId = "user-creator";

        const int concurrencyCount = 10;
        var tasks = Enumerable.Range(1, concurrencyCount).Select(async i =>
        {
            using var localContext = CreateOfficeContext(dbName);
            var localService = CreateService(localContext, CreateUserManager());
            var model = new ContractCreateVM
            {
                Title = $"Contract {i}",
                ClientName = $"Client {i}",
                TotalAmount = 1000m * i,
                StartDate = DateTime.Today
            };
            return await localService.CreateContractAsync(model, userId, CancellationToken.None);
        });

        var results = await Task.WhenAll(tasks);
        var numbers = results.Select(c => c.ContractNumber).ToList();

        // Verify all generated contract numbers are strictly unique
        Assert.Equal(concurrencyCount, numbers.Distinct().Count());

        var pc = new System.Globalization.PersianCalendar();
        int year = pc.GetYear(DateTime.Now);
        foreach (var num in numbers)
        {
            Assert.StartsWith($"CTR-{year}-", num);
        }
    }

    // ── Magic-byte file upload validation tests (Task 3.3) ──
    [Fact]
    public void FileSignatureValidator_AllowedExtensions_ContainsAllExpectedTypes()
    {
        var expected = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".txt", ".zip" };
        foreach (var ext in expected)
        {
            Assert.Contains(ext, OfficeAutomation.Services.Security.FileSignatureValidator.AllowedExtensions);
        }
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".sh")]
    [InlineData(".ps1")]
    [InlineData(".dll")]
    [InlineData(".jsp")]
    [InlineData(".asp")]
    [InlineData(".aspx")]
    [InlineData(".html")]
    [InlineData(".svg")]
    public void FileSignatureValidator_IsDangerousExtension_ReturnsTrue_ForDangerousExtensions(string ext)
    {
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsDangerousExtension(ext));
    }

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".png")]
    [InlineData(".jpg")]
    public void FileSignatureValidator_IsDangerousExtension_ReturnsFalse_ForSafeExtensions(string ext)
    {
        Assert.False(OfficeAutomation.Services.Security.FileSignatureValidator.IsDangerousExtension(ext));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_ValidPdf_ReturnsTrue()
    {
        byte[] pdfMagic = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37]; // %PDF-1.7
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".pdf", pdfMagic));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_ValidPng_ReturnsTrue()
    {
        byte[] pngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".png", pngMagic));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_ValidJpeg_ReturnsTrue()
    {
        byte[] jpgMagic = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".jpg", jpgMagic));
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".jpeg", jpgMagic));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_ValidOpenXml_DocxAndXlsx_ReturnsTrue()
    {
        byte[] zipMagic = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00];
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".docx", zipMagic));
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".xlsx", zipMagic));
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".zip", zipMagic));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_ValidCompoundFile_DocAndXls_ReturnsTrue()
    {
        byte[] oleMagic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".doc", oleMagic));
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".xls", oleMagic));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_ValidWebP_ReturnsTrue()
    {
        byte[] webpMagic = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]; // RIFF....WEBP
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".webp", webpMagic));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_ValidText_ReturnsTrue()
    {
        byte[] textData = System.Text.Encoding.UTF8.GetBytes("This is plain text content for testing.");
        Assert.True(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".txt", textData));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_BinaryDisguisedAsText_ReturnsFalse()
    {
        byte[] nullByteData = [0x41, 0x42, 0x00, 0x43]; // contains null byte
        Assert.False(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".txt", nullByteData));
    }

    [Fact]
    public void FileSignatureValidator_IsSignatureValid_ExecutableRenamedToDocx_ReturnsFalse()
    {
        // MZ header (DOS/PE Executable)
        byte[] exeMagic = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
        Assert.False(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".docx", exeMagic));
        Assert.False(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".pdf", exeMagic));
        Assert.False(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".png", exeMagic));
        Assert.False(OfficeAutomation.Services.Security.FileSignatureValidator.IsSignatureValid(".xlsx", exeMagic));
    }

    [Fact]
    public async Task FileSignatureValidator_ValidateUploadAsync_ExeDisguisedAsPdf_ReturnsPersianError()
    {
        byte[] fakePdfBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        using var stream = new MemoryStream(fakePdfBytes);

        var error = await OfficeAutomation.Services.Security.FileSignatureValidator.ValidateUploadAsync(
            "malicious.pdf",
            "application/pdf",
            fakePdfBytes.Length,
            stream);

        Assert.NotNull(error);
        Assert.Equal("امضای باینری فایل (Magic-Byte) با نوع فایل مطابقت ندارد. فایل نامعتبر یا دستکاری شده است.", error);
    }

    [Fact]
    public async Task FileSignatureValidator_ValidateUploadAsync_ValidPdf_ReturnsNull()
    {
        byte[] validPdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35, 0x0A, 0x25, 0xC7, 0xEC, 0x8F, 0xA2, 0x0A, 0x31];
        using var stream = new MemoryStream(validPdf);

        var error = await OfficeAutomation.Services.Security.FileSignatureValidator.ValidateUploadAsync(
            "document.pdf",
            "application/pdf",
            validPdf.Length,
            stream);

        Assert.Null(error);
        Assert.Equal(0, stream.Position); // ensures stream position was reset
    }

    [Fact]
    public async Task FileSignatureValidator_ValidateUploadAsync_DisallowedExtension_ReturnsPersianError()
    {
        byte[] bytes = [0x01, 0x02, 0x03, 0x04];
        using var stream = new MemoryStream(bytes);

        var error = await OfficeAutomation.Services.Security.FileSignatureValidator.ValidateUploadAsync(
            "script.ps1",
            "text/plain",
            bytes.Length,
            stream);

        Assert.NotNull(error);
        Assert.Contains("فرمت فایل مجاز نیست", error);
    }

    [Fact]
    public async Task FileSignatureValidator_ValidateUploadAsync_DisallowedMimeType_ReturnsPersianError()
    {
        byte[] validPdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35, 0x0A, 0x25, 0xC7, 0xEC, 0x8F, 0xA2, 0x0A, 0x31];
        using var stream = new MemoryStream(validPdf);

        var error = await OfficeAutomation.Services.Security.FileSignatureValidator.ValidateUploadAsync(
            "document.pdf",
            "application/x-msdownload", // disallowed mime
            validPdf.Length,
            stream);

        Assert.NotNull(error);
        Assert.Contains("MIME-Type", error);
    }

    [Fact]
    public async Task ContractService_UploadAttachmentAsync_FakeDocx_ThrowsPersianException()
    {
        using var context = CreateOfficeContext(nameof(ContractService_UploadAttachmentAsync_FakeDocx_ThrowsPersianException));
        var userId = "user-uploader";
        var contract = await SeedContractAsync(context, userId);
        var service = CreateService(context, CreateUserManager());

        byte[] fakeDocxBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]; // EXE bytes
        using var stream = new MemoryStream(fakeDocxBytes);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.UploadAttachmentAsync(
                contract.Id,
                "fake-contract.docx",
                stream,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                userId);
        });

        Assert.Equal("امضای باینری فایل (Magic-Byte) با نوع فایل مطابقت ندارد. فایل نامعتبر یا دستکاری شده است.", ex.Message);
    }

    [Theory]
    [InlineData(nameof(ContractsController.DownloadAttachment))]
    [InlineData(nameof(ContractsController.DownloadAttachmentVersion))]
    public void ContractsController_DownloadEndpoints_HaveAttachmentDownloadRateLimiting(string methodName)
    {
        var method = typeof(ContractsController).GetMethod(methodName);
        Assert.NotNull(method);

        var rateLimitAttr = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), true)
            .Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
            .FirstOrDefault();

        Assert.NotNull(rateLimitAttr);
        Assert.Equal("attachment-download", rateLimitAttr.PolicyName);
    }

    [Fact]
    public async Task ContractService_AddAuditLogAsync_MasksPhoneAndAddressInDescription()
    {
        using var context = CreateOfficeContext(nameof(ContractService_AddAuditLogAsync_MasksPhoneAndAddressInDescription));
        var userId = "user-auditor";
        var contract = await SeedContractAsync(context, userId);
        var maskingService = new SecurityFieldMaskingService(new Mock<ICurrentUserContextAccessor>().Object);
        var service = new ContractService(
            context,
            CreateUserManager(),
            NullLogger<ContractService>.Instance,
            new Mock<IAuditLogger>().Object,
            new Mock<ITemplateRenderingService>().Object,
            new Mock<ITenantSettingsService>().Object,
            new Mock<IVersionService>().Object,
            new Mock<IContractAccountingService>().Object,
            new Mock<IPaymentPenaltyService>().Object,
            new Mock<IDocumentRequirementService>().Object,
            maskingService);

        var description = "تماس با شماره 09123456789 جهت بررسی قرارداد و ارسال به آدرس تهران، خیابان آزادی، پلاک 12 واحد 4 و ایمیل client@company.ir";

        await service.AddAuditLogAsync(contract.Id, userId, "NotesAdded", null, null, description);

        var log = await context.ContractAuditLogs.FirstOrDefaultAsync(l => l.ContractId == contract.Id && l.Action == "NotesAdded");
        Assert.NotNull(log);
        Assert.DoesNotContain("09123456789", log.Description);
        Assert.Contains("091****89", log.Description);
        Assert.DoesNotContain("خیابان آزادی، پلاک 12", log.Description);
        Assert.Contains("[آدرس ماسک شد]", log.Description);
        Assert.DoesNotContain("client@company.ir", log.Description);
        Assert.Contains("cl***@company.ir", log.Description);
    }
}