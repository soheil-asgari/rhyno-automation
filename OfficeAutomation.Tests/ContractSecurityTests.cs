using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Contracts;
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
        var logger = NullLogger<ContractService>.Instance;
        return new ContractService(context, userManager, logger, auditLogger.Object, templateRenderer.Object, tenantSettings.Object);
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
}