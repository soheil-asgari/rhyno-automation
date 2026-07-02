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

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OfficeDbContext).Assembly,
            type => type.Namespace?.StartsWith("OfficeAutomation.Modules.Office.", StringComparison.Ordinal) == true);
    }
}
