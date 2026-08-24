using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence;

public sealed class OfficeDbContext : ModularDbContext
{
    public OfficeDbContext(
        DbContextOptions<OfficeDbContext> options,
        ITenantIsolationService? tenantIsolationService = null)
        : base(options, tenantIsolationService)
    {
    }

    public DbSet<Letter> Letters => Set<Letter>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<HumanCapitalEmployee> HumanCapitalEmployees => Set<HumanCapitalEmployee>();
    public DbSet<HumanCapitalSalaryHistory> HumanCapitalSalaryHistories => Set<HumanCapitalSalaryHistory>();
    public DbSet<HumanCapitalStatusHistory> HumanCapitalStatusHistories => Set<HumanCapitalStatusHistory>();
    public DbSet<OrganizationCalendarEvent> OrganizationCalendarEvents => Set<OrganizationCalendarEvent>();
    public DbSet<DocumentArchiveItem> DocumentArchiveItems => Set<DocumentArchiveItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertSubscription> AlertSubscriptions => Set<AlertSubscription>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();

    // ── Human Capital Sub-Modules ─────────────────────────────────
    public DbSet<HrContract> HrContracts => Set<HrContract>();
    public DbSet<HrLeave> HrLeaves => Set<HrLeave>();
    public DbSet<HrLoan> HrLoans => Set<HrLoan>();
    public DbSet<HrAttendance> HrAttendances => Set<HrAttendance>();

    // ── Customers Module ──────────────────────────────────────────
    public DbSet<Customer> Customers => Set<Customer>();

    // ── Contracts Module ──────────────────────────────────────────
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractTemplate> ContractTemplates => Set<ContractTemplate>();
    public DbSet<ContractPayment> ContractPayments => Set<ContractPayment>();
    public DbSet<ContractAttachment> ContractAttachments => Set<ContractAttachment>();
    public DbSet<ContractUserSetting> ContractUserSettings => Set<ContractUserSetting>();
    public DbSet<ContractAuditLog> ContractAuditLogs => Set<ContractAuditLog>();

    // ── Approval Workflow Module ───────────────────────────────────
    public DbSet<ContractApprovalRequest> ContractApprovalRequests => Set<ContractApprovalRequest>();
    public DbSet<ContractApprovalStep> ContractApprovalSteps => Set<ContractApprovalStep>();
    public DbSet<ContractApprovalDelegation> ContractApprovalDelegations => Set<ContractApprovalDelegation>();

    // ── Contract Versioning Module ──────────────────────────────────
    public DbSet<ContractVersion> ContractVersions => Set<ContractVersion>();
// ── Payment Schedule Module ───────────────────────────────────────────
    public DbSet<ContractPaymentSchedule> PaymentSchedules => Set<ContractPaymentSchedule>();
    public DbSet<ContractPaymentScheduleInstallment> PaymentScheduleInstallments => Set<ContractPaymentScheduleInstallment>();

    // ── Amendment Module ──────────────────────────────────────────────
    public DbSet<ContractAmendment> ContractAmendments => Set<ContractAmendment>();

    // ── Tax / VAT Module ──────────────────────────────────────────────────
    public DbSet<ContractTaxLine> ContractTaxLines => Set<ContractTaxLine>();

// ── Attachment Versioning Module ─────────────────────────────────
    public DbSet<ContractAttachmentVersion> ContractAttachmentVersions => Set<ContractAttachmentVersion>();
    // ── E-Signature Module ──────────────────────────────────────────────
    public DbSet<ContractSignatureRequest> ContractSignatureRequests => Set<ContractSignatureRequest>();
    public DbSet<ContractSignatureSigner> ContractSignatureSigners => Set<ContractSignatureSigner>();
    public DbSet<ContractSignatureAuditLog> ContractSignatureAuditLogs => Set<ContractSignatureAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyTenantSchema(modelBuilder);

        modelBuilder.Ignore<IdentityUserRole<string>>();
        modelBuilder.Ignore<IdentityUserClaim<string>>();
        modelBuilder.Ignore<IdentityUserLogin<string>>();
        modelBuilder.Ignore<IdentityUserToken<string>>();
        modelBuilder.Ignore<IdentityRoleClaim<string>>();
        modelBuilder.Ignore<ApplicationRole>();

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("AspNetUsers");
            builder.Ignore(item => item.Department);
            builder.Ignore(item => item.Manager);
            builder.Ignore(item => item.ParentManagerUser);
            builder.Ignore(item => item.Employee);
        });
        modelBuilder.Entity<Department>(builder =>
        {
            builder.Ignore(item => item.Manager);
            builder.Ignore(item => item.ManagerEmployee);
            builder.Ignore(item => item.Users);
        });

        // ── Contract Configuration ─────────────────────────────────
        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("Contracts");

            entity.HasIndex(c => c.ContractNumber).IsUnique();
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.AssignedUserId);
            entity.HasIndex(c => c.CreatedByUserId);
            entity.HasIndex(c => c.IsArchived);
            entity.HasIndex(c => c.CurrencyCode);
            entity.HasIndex(c => c.TaxMethod);

            entity.Property(c => c.RemainingBalance)
                .HasComputedColumnSql("TotalAmount - PaidAmount");

            entity.Property(c => c.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired()
                .HasDefaultValue("IRR");

            entity.Property(c => c.ExchangeRate)
                .HasDefaultValue(1m);

            entity.Property(c => c.TaxMethod)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue(OfficeAutomation.Models.TaxCalculationMethod.Exclusive);

            entity.HasOne(c => c.Customer)
                .WithMany(cust => cust.Contracts)
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.AssignedUser)
                .WithMany()
                .HasForeignKey(c => c.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.CreatedByUser)
                .WithMany()
                .HasForeignKey(c => c.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.ArchivedByUser)
                .WithMany()
                .HasForeignKey(c => c.ArchivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.ContractTemplate)
                .WithMany(t => t.Contracts)
                .HasForeignKey(c => c.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(c => c.Payments)
                .WithOne(p => p.Contract)
                .HasForeignKey(p => p.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Attachments)
                .WithOne(a => a.Contract)
                .HasForeignKey(a => a.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.AuditLogs)
                .WithOne(al => al.Contract)
                .HasForeignKey(al => al.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ContractPayment Configuration ──────────────────────────
        modelBuilder.Entity<ContractPayment>(entity =>
        {
            entity.ToTable("ContractPayments");
            entity.HasIndex(p => p.ContractId);
        });

        // ── ContractTemplate Configuration ─────────────────────────
        modelBuilder.Entity<ContractTemplate>(entity =>
        {
            entity.ToTable("ContractTemplates");
            entity.HasIndex(t => t.OwnerUserId);

            entity.HasOne(t => t.OwnerUser)
                .WithMany()
                .HasForeignKey(t => t.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── ContractAttachment Configuration ───────────────────────
        modelBuilder.Entity<ContractAttachment>(entity =>
        {
            entity.ToTable("ContractAttachments");
            entity.HasIndex(a => a.ContractId);
        });

        // ── ContractAuditLog Configuration ─────────────────────────
        modelBuilder.Entity<ContractAuditLog>(entity =>
        {
            entity.ToTable("ContractAuditLogs");
            entity.HasIndex(al => al.ContractId);
            entity.HasIndex(al => al.UserId);

            entity.HasOne(al => al.User)
                .WithMany()
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ContractUserSetting Configuration ──────────────────────
        modelBuilder.Entity<ContractUserSetting>(entity =>
        {
            entity.ToTable("ContractUserSettings");
            entity.HasIndex(s => s.UserId).IsUnique();
        });

        // ── ContractTaxLine Configuration ─────────────────────────
        modelBuilder.Entity<ContractTaxLine>(entity =>
        {
            entity.ToTable("ContractTaxLines");
            entity.HasIndex(t => t.ContractId);
            entity.HasIndex(t => t.TaxCategory);

            entity.Property(t => t.TaxName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(t => t.CalculationMethod)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(t => t.TaxCategory)
                .HasMaxLength(20);

            entity.HasOne(t => t.Contract)
                .WithMany(c => c.TaxLines)
                .HasForeignKey(t => t.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Amendment)
                .WithMany()
                .HasForeignKey(t => t.AmendmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── E-Signature Configuration ─────────────────────────────────────
        modelBuilder.Entity<ContractSignatureRequest>(entity =>
        {
            entity.ToTable("ContractSignatureRequests");
            entity.HasKey(r => r.Id);

            entity.HasIndex(r => r.ContractId);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.AccessToken).IsUnique().HasFilter("[AccessToken] IS NOT NULL");

            entity.Property(r => r.Title).HasMaxLength(200).IsRequired();
            entity.Property(r => r.Message).HasMaxLength(2000);
            entity.Property(r => r.Status).HasMaxLength(20).IsRequired();
            entity.Property(r => r.DeliveryMethod).HasMaxLength(20).IsRequired();
            entity.Property(r => r.SigningOrder).HasMaxLength(20).IsRequired();
            entity.Property(r => r.AccessToken).HasMaxLength(128);

            entity.HasOne(r => r.Contract)
                .WithMany()
                .HasForeignKey(r => r.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Amendment)
                .WithMany()
                .HasForeignKey(r => r.AmendmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(r => r.Signers)
                .WithOne(s => s.SignatureRequest)
                .HasForeignKey(s => s.SignatureRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(r => r.AuditLogs)
                .WithOne(l => l.SignatureRequest)
                .HasForeignKey(l => l.SignatureRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractSignatureSigner>(entity =>
        {
            entity.ToTable("ContractSignatureSigners");
            entity.HasKey(s => s.Id);

            entity.HasIndex(s => s.SignatureRequestId);
            entity.HasIndex(s => s.UserId);
            entity.HasIndex(s => s.SignerToken).IsUnique().HasFilter("[SignerToken] IS NOT NULL");
            entity.HasIndex(s => s.Result);

            entity.Property(s => s.SignerType).HasMaxLength(20).IsRequired();
            entity.Property(s => s.UserId).HasMaxLength(450);
            entity.Property(s => s.FullName).HasMaxLength(150);
            entity.Property(s => s.Email).HasMaxLength(200);
            entity.Property(s => s.PhoneNumber).HasMaxLength(20);
            entity.Property(s => s.Organization).HasMaxLength(200);
            entity.Property(s => s.JobTitle).HasMaxLength(100);
            entity.Property(s => s.Result).HasMaxLength(20).IsRequired();
            entity.Property(s => s.SignerToken).HasMaxLength(128);
            entity.Property(s => s.SignedIpAddress).HasMaxLength(45);
            entity.Property(s => s.SignedUserAgent).HasMaxLength(500);
            entity.Property(s => s.Comment).HasMaxLength(2000);
            entity.Property(s => s.SentByUserId).HasMaxLength(450);
            entity.Property(s => s.CertificateThumbprint).HasMaxLength(100);
            entity.Property(s => s.SignatureImageData).HasColumnType("nvarchar(max)").IsRequired(false);

            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.SentByUser)
                .WithMany()
                .HasForeignKey(s => s.SentByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ContractSignatureAuditLog>(entity =>
        {
            entity.ToTable("ContractSignatureAuditLogs");
            entity.HasKey(l => l.Id);

            entity.HasIndex(l => l.SignatureRequestId);
            entity.HasIndex(l => l.SignerId);
            entity.HasIndex(l => l.EventType);

            entity.Property(l => l.EventType).HasMaxLength(30).IsRequired();
            entity.Property(l => l.Description).HasMaxLength(1000);
            entity.Property(l => l.PerformedByUserId).HasMaxLength(450);
            entity.Property(l => l.IpAddress).HasMaxLength(45);
            entity.Property(l => l.Metadata).HasMaxLength(2000);

            entity.HasOne(l => l.Signer)
                .WithMany()
                .HasForeignKey(l => l.SignerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(l => l.PerformedByUser)
                .WithMany()
                .HasForeignKey(l => l.PerformedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ── Human Capital Sub-Modules Configuration ────────────────
        modelBuilder.Entity<HrContract>(entity =>
        {
            entity.ToTable("HrContracts");
            entity.HasIndex(c => c.EmployeeId);
            entity.HasIndex(c => c.IsActive);
            entity.HasQueryFilter(c => !c.IsDeleted);
            entity.HasOne(c => c.Employee)
                .WithMany()
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HrLeave>(entity =>
        {
            entity.ToTable("HrLeaves");
            entity.HasIndex(l => l.EmployeeId);
            entity.HasIndex(l => l.Status);
            entity.HasQueryFilter(l => !l.IsDeleted);
            entity.HasOne(l => l.Employee)
                .WithMany()
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HrLoan>(entity =>
        {
            entity.ToTable("HrLoans");
            entity.HasIndex(l => l.EmployeeId);
            entity.HasIndex(l => l.Status);
            entity.HasQueryFilter(l => !l.IsDeleted);
            entity.HasOne(l => l.Employee)
                .WithMany()
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HrAttendance>(entity =>
        {
            entity.ToTable("HrAttendances");
            entity.HasIndex(a => a.EmployeeId);
            entity.HasIndex(a => new { a.Year, a.Month });
            entity.HasQueryFilter(a => !a.IsDeleted);
            entity.HasOne(a => a.Employee)
                .WithMany()
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ContractApprovalRequest Configuration ────────────────────
        modelBuilder.Entity<ContractApprovalRequest>(entity =>
        {
            entity.ToTable("ContractApprovalRequests");
            entity.HasIndex(r => r.ContractId);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.RequestedByUserId);

            entity.HasOne(r => r.Contract)
                .WithMany()
                .HasForeignKey(r => r.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.RequestedByUser)
                .WithMany()
                .HasForeignKey(r => r.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.Steps)
                .WithOne(s => s.ContractApprovalRequest)
                .HasForeignKey(s => s.ContractApprovalRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ContractApprovalStep Configuration ────────────────────────
        modelBuilder.Entity<ContractApprovalStep>(entity =>
        {
            entity.ToTable("ContractApprovalSteps");
            entity.HasIndex(s => s.ContractApprovalRequestId);
            entity.HasIndex(s => s.AssignedUserId);
            entity.HasIndex(s => s.Decision);

            entity.HasOne(s => s.AssignedUser)
                .WithMany()
                .HasForeignKey(s => s.AssignedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.DecidedByUser)
                .WithMany()
                .HasForeignKey(s => s.DecidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.DelegatedToUser)
                .WithMany()
                .HasForeignKey(s => s.DelegatedToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ContractApprovalDelegation Configuration ──────────────────
        modelBuilder.Entity<ContractApprovalDelegation>(entity =>
        {
            entity.ToTable("ContractApprovalDelegations");
            entity.HasIndex(d => d.FromUserId);
            entity.HasIndex(d => d.ToUserId);
            entity.HasIndex(d => d.IsActive);

            entity.HasOne(d => d.FromUser)
                .WithMany()
                .HasForeignKey(d => d.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ToUser)
                .WithMany()
                .HasForeignKey(d => d.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);
// ── ContractAttachment Versioning Configuration ────────────────
        modelBuilder.Entity<ContractAttachmentVersion>(entity =>
        {
            entity.ToTable("ContractAttachmentVersions");
            entity.HasKey(v => v.Id);

            entity.HasIndex(v => v.AttachmentId);
            entity.HasIndex(v => new { v.AttachmentId, v.VersionNumber }).IsUnique();

            entity.Property(v => v.FileName).HasMaxLength(260).IsRequired();
            entity.Property(v => v.StoredFileName).HasMaxLength(260).IsRequired();
            entity.Property(v => v.RelativePath).HasMaxLength(400).IsRequired();
            entity.Property(v => v.ContentType).HasMaxLength(120);
            entity.Property(v => v.ChangeDescription).HasMaxLength(1000);
            entity.Property(v => v.UploadedByUserId).HasMaxLength(450).IsRequired();

            entity.HasOne(v => v.Attachment)
                .WithMany(a => a.Versions)
                .HasForeignKey(v => v.AttachmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.UploadedByUser)
                .WithMany()
                .HasForeignKey(v => v.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ContractAttachment configuration (update for CurrentVersionId) ──
        modelBuilder.Entity<ContractAttachment>(entity =>
        {
            entity.HasIndex(a => a.CurrentVersionId);

            entity.HasOne(a => a.CurrentVersion)
                .WithMany()
                .HasForeignKey(a => a.CurrentVersionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ContractVersion Configuration ────────────────────────────
        });

        // ── ContractVersion Configuration ────────────────────────────
        modelBuilder.Entity<ContractVersion>(entity =>
        {
            entity.ToTable("ContractVersions");
            entity.HasKey(v => v.Id);

            entity.HasIndex(v => v.ContractId);
            entity.HasIndex(v => new { v.ContractId, v.MajorVersion, v.MinorVersion })
                .IsUnique();

            entity.Property(v => v.SnapshotData)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(v => v.ChangeType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(v => v.ChangeDescription)
                .HasMaxLength(2000);

            entity.Property(v => v.CreatedByUserId)
                .HasMaxLength(450)
                .IsRequired();

            entity.HasOne(v => v.Contract)
                .WithMany()
                .HasForeignKey(v => v.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.CreatedByUser)
                .WithMany()
                .HasForeignKey(v => v.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ContractAmendment Configuration ──────────────────────────
        modelBuilder.Entity<ContractAmendment>(entity =>
        {
            entity.ToTable("ContractAmendments");
            entity.HasKey(a => a.Id);

            entity.HasIndex(a => a.ContractId);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => new { a.ContractId, a.AmendmentNumber }).IsUnique();

            entity.Property(a => a.AmendmentType).HasMaxLength(20).IsRequired();
            entity.Property(a => a.Title).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Description).HasMaxLength(4000);
            entity.Property(a => a.Status).HasMaxLength(20).IsRequired();
            entity.Property(a => a.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(a => a.AppliedByUserId).HasMaxLength(450);

            entity.HasOne(a => a.Contract)
                .WithMany(c => c.Amendments)
                .HasForeignKey(a => a.ContractId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.CreatedByUser)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.AppliedByUser)
                .WithMany()
                .HasForeignKey(a => a.AppliedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.ApprovalRequest)
                .WithMany()
                .HasForeignKey(a => a.ApprovalRequestId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OfficeDbContext).Assembly,
            type => type.Namespace?.StartsWith("OfficeAutomation.Modules.Office.", StringComparison.Ordinal) == true);
    }
}
