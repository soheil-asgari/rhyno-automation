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
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();

    // ── Customers Module ──────────────────────────────────────────
    public DbSet<Customer> Customers => Set<Customer>();

    // ── Contracts Module ──────────────────────────────────────────
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractTemplate> ContractTemplates => Set<ContractTemplate>();
    public DbSet<ContractPayment> ContractPayments => Set<ContractPayment>();
    public DbSet<ContractAttachment> ContractAttachments => Set<ContractAttachment>();
    public DbSet<ContractUserSetting> ContractUserSettings => Set<ContractUserSetting>();
    public DbSet<ContractAuditLog> ContractAuditLogs => Set<ContractAuditLog>();

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

            entity.Property(c => c.RemainingBalance)
                .HasComputedColumnSql("TotalAmount - PaidAmount");

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

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OfficeDbContext).Assembly,
            type => type.Namespace?.StartsWith("OfficeAutomation.Modules.Office.", StringComparison.Ordinal) == true);
    }
}
